using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YourFritz.TFFS;

namespace YourFritz.EVA
{
    // EVA specific exception
    public class EVAClientException : FTPClientException
    {
        internal EVAClientException()
        {
        }

        internal EVAClientException(string message)
            : base(message)
        {
        }

        internal EVAClientException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    // EVA specific FTP client class
    //
    // It uses strictly the TPL and tries to avoid blocking in most calls.
    // Use the provided events to get notified on commands sent, responses received and commands completed.
    public class EVAClient : FTPClient
    {
        public new event EventHandler<ActionCompletedEventArgs> ActionCompleted;

        private bool p_IsLoggedIn = false;
        private bool p_IgnoreLoginError = false;
        private string p_User = EVADefaults.EVADefaultUser;
        private string p_Password = EVADefaults.EVADefaultPassword;
        private EVAMediaType p_MediaType = EVAMediaType.RAM;

        private List<Task> outstandingEventHandlers = new List<Task>();
        private TFFSNameTable nameTable = TFFSNameTable.GetLatest();
        private EVACommands commands = EVACommandFactory.GetCommands();
        private EVAResponses responses = EVAResponseFactory.GetResponses();
        private int goodbyeCode = EVAResponseFactory.GetResponses().FindFlag(EVAResponseFlags.GoodbyeMessage).Code;
        private int notLoggedInCode = EVAResponseFactory.GetResponses().FindFlag(EVAResponseFlags.NotLoggedIn).Code;


        public EVAClient() :
            base(EVADefaults.EVADefaultIP)
        {
            Initialize();
        }

        public EVAClient(string Address) :
            base(Address)
        {
            Initialize();
        }

        public EVAClient(string Address, int Port) :
            base(Address, Port)
        {
            Initialize();
        }

        public EVAClient(IPAddress Address) :
            base(Address)
        {
            Initialize();
        }

        public EVAClient(IPAddress Address, int Port) :
            base(Address, Port)
        {
            Initialize();
        }

        public string User
        {
            get
            {
                return p_User;
            }
            set
            {
                p_User = value;
            }
        }

        public string Password
        {
            get
            {
                return p_Password;
            }
            set
            {
                p_Password = value;
            }
        }

        public bool IsLoggedIn
        {
            get
            {
                return p_IsLoggedIn;
            }
        }

        public bool IgnoreLoginErrors
        {
            get
            {
                return p_IgnoreLoginError;
            }
            set
            {
                p_IgnoreLoginError = value;
            }
        }

        public EVAMediaType MediaType
        {
            get
            {
                return p_MediaType;
            }
            set
            {
                p_MediaType = value;
            }
        }

        internal override object SyncRoot
        {
            get
            {
                return p_Lock;
            }
        }

        public override async Task OpenAsync()
        {
            await base.OpenAsync();
        }

        public override async Task OpenAsync(string Address)
        {
            await base.OpenAsync(Address);
        }

        public override async Task OpenAsync(string Address, int Port)
        {
            await base.OpenAsync(Address, Port);
        }

        public override async Task OpenAsync(IPAddress Address)
        {
            await base.OpenAsync(Address);
        }

        public override async Task OpenAsync(IPAddress Address, int Port)
        {
            await base.OpenAsync(Address, Port);
        }

        public async override Task CloseAsync()
        {
            await CompleteEventsAsync();

            if (IsLoggedIn)
            {
                await LogoutAsync();
            }

            await base.CloseAsync();

            await CompleteEventsAsync();
        }

        internal async Task CompleteEventsAsync(int Timeout = 1000)
        {
            if (RemoveFinishedEventHandlerTasks() == 0)
            {
                return;
            }

            Task[] waitForOutstandingHandlers = new Task[2];

            // wait the specified time for all outstanding event handlers to finish
            waitForOutstandingHandlers[0] = Task.Run(() => Task.WhenAll(outstandingEventHandlers));
            waitForOutstandingHandlers[1] = Task.Delay(Timeout);

            await Task.WhenAny(waitForOutstandingHandlers);

            RemoveFinishedEventHandlerTasks();
        }

        public async override Task<FTPAction> RunCommandAsync(string Command)
        {
            return await RunCommandAsync(Command, null);
        }

        public async override Task<FTPAction> RunCommandAsync(string Command, object UserData)
        {
            await CompleteEventsAsync();

            return await base.RunCommandAsync(Command, UserData);
        }

        public async Task<FTPAction> RunCommandAndCheckResultAsync(string Command, int Code)
        {
            return await RunCommandAndCheckResultAsync(Command, Code, null);
        }


        public async Task<FTPAction> RunCommandAndCheckResultAsync(string Command, int Code, object UserData)
        {
            Task<FTPAction> runCmdTask = RunCommandAsync(Command, UserData);

            FTPAction runCmd = await runCmdTask;

            //if (runCmd == null)
            //{
            //    Debug.WriteLine("runCmd is null");
            //}

            await CompleteEventsAsync();

            await runCmd.AsyncTask;

            runCmd.ExpectedAnswer = Code;

            if (runCmd.AsyncTask.IsCompleted && runCmd.AsyncTask.Exception != null && runCmd.AsyncTask.Exception is AggregateException)
            {
                throw runCmd.AsyncTask.Exception.InnerException;
            }
            else if (runCmd.AsyncTask.Exception != null)
            {
                throw runCmd.AsyncTask.Exception;
            }

            return runCmd;
        }

        public async Task LoginAsync(string User, string Password)
        {
            p_User = User;
            p_Password = Password;
            await LoginAsync();
        }

        public async Task LoginAsync()
        {
            if (p_User.Length == 0 || p_Password.Length == 0)
            {
                throw new EVAClientException("Missing user name and/or password for login.");
            }

            FTPAction login = await RunCommandAndCheckResultAsync(
                String.Format(commands[EVACommandType.User].CommandValue, p_User),
                responses.FindFlag(EVAResponseFlags.PasswordRequired).Code,
                commands[EVACommandType.User]
                );

            await login.AsyncTask;

            if (login.Success)
            {
                await CompleteEventsAsync();

                FTPAction pw = await RunCommandAndCheckResultAsync(
                    String.Format(commands[EVACommandType.Password].CommandValue, p_Password),
                    responses.FindFlag(EVAResponseFlags.LoggedIn).Code,
                    commands[EVACommandType.Password]
                    );

                await pw.AsyncTask;

                if (pw.Success)
                {
                    p_IsLoggedIn = true;
                    return;
                }
                else
                {
                    throw new EVAClientException(String.Format("Login failed: {0:s}", pw.Response.ToString()));
                }
            }
            else
            {
                throw new EVAClientException(String.Format("Login failed: {0:s}", login.Response.ToString()));
            }
        }

        public async Task LogoutAsync()
        {
            p_IsLoggedIn = false;
            if (IsConnected)
            {
                FTPAction quit = await RunCommandAndCheckResultAsync(
                    commands[EVACommandType.Quit].CommandValue,
                    responses.FindFlag(EVAResponseFlags.GoodbyeMessage).Code,
                    commands[EVACommandType.Quit]
                    );

                await quit.AsyncTask;

                await CompleteEventsAsync();
            }
        }

        public async Task EnsureItIsEVAAsync()
        {
            FTPAction syst = await RunCommandAndCheckResultAsync(
                commands[EVACommandType.SystemType].CommandValue,
                responses.FindFlag(EVAResponseFlags.Identity).Code,
                commands[EVACommandType.SystemType]
                );

            await syst.AsyncTask;

            if (syst.Success)
            {
                EVAResponse response = responses.FindResponse(syst.Response.Code, syst.Response.Message);

                if (response == null || (response.Flags & EVAResponseFlags.Identity) == 0)
                {
                    throw new EVAClientException(String.Format("Unexpected system type: {0:s}", syst.Response.ToString()));
                }
            }
            else
            {
                throw new EVAClientException(String.Format("Unexpected error returned: {0:s}", syst.Response.ToString()));
            }
        }

        public async Task RebootAsync()
        {
            if (IsConnected)
            {
                FTPAction reboot = await RunCommandAndCheckResultAsync(
                    commands[EVACommandType.Reboot].CommandValue,
                    responses.FindFlag(EVAResponseFlags.GoodbyeMessage).Code,
                    commands[EVACommandType.Reboot]
                    );

                await reboot.AsyncTask;
            }
            else
            {
                throw new EVAClientException("The connection is not open.");
            }
        }

        public async Task<string> GetEnvironmentValueAsync(string Name)
        {
            return await GetEnvironmentValueAsync(Name, nameTable);
        }

        public async Task<string> GetEnvironmentValueAsync(string Name, TFFSNameTable NameTable)
        {
            CheckName(Name, NameTable);

            FTPAction getenv = await RunCommandAndCheckResultAsync(
                String.Format(commands[EVACommandType.GetEnvironmentValue].CommandValue, Name),
                responses.CodeForSuccess,
                commands[EVACommandType.GetEnvironmentValue]
                );

            string output = null;

            await getenv.AsyncTask;

            if (getenv.Success)
            {
                // do not use a Regex here, there are variables with a value length of zero
                ((MultiLineResponse)getenv.Response).Content.ForEach((e) =>
                {
                    if (e.Length >= Name.Length && e.StartsWith(Name))
                    {
                        output = output ?? e.Substring(Name.Length + 1).TrimStart();
                    }
                });
            }
            else
            {
                EVAResponse response = responses.FindResponse(getenv.Response.Code, getenv.Response.Message);

                if ((response.Flags & EVAResponseFlags.VariableNotSet) == 0)
                {
                    throw new EVAClientException(String.Format("Unexpected error returned: {0:s}", getenv.Response.ToString()));
                }
            }

            return output;
        }

        public async Task RemoveEnvironmentValueAsync(string Name)
        {
            await RemoveEnvironmentValueAsync(Name, nameTable);
        }

        public async Task RemoveEnvironmentValueAsync(string Name, TFFSNameTable NameTable)
        {
            CheckName(Name, NameTable);

            FTPAction unsetenv = await RunCommandAndCheckResultAsync(
                String.Format(commands[EVACommandType.UnsetEnvironmentValue].CommandValue, Name),
                responses.CodeForSuccess,
                commands[EVACommandType.UnsetEnvironmentValue]
                );

            await unsetenv.AsyncTask;

            if (!unsetenv.Success)
            {
                throw new EVAClientException(String.Format("Unexpected error returned: {0:s}", unsetenv.Response.ToString()));
            }
        }

        public async Task SetEnvironmentValueAsync(string Name, string Value)
        {
            await SetEnvironmentValueAsync(Name, Value, nameTable);
        }

        public async Task SetEnvironmentValueAsync(string Name, string Value, TFFSNameTable NameTable)
        {
            CheckName(Name, NameTable);

            FTPAction setenv = await RunCommandAndCheckResultAsync(
                String.Format(commands[EVACommandType.SetEnvironmentValue].CommandValue, Name, Value),
                responses.CodeForSuccess,
                commands[EVACommandType.SetEnvironmentValue]
                );

            await setenv.AsyncTask;

            if (!setenv.Success)
            {
                throw new EVAClientException(String.Format("Unexpected error returned: {0:s}", setenv.Response.ToString()));
            }
        }

        public async Task SwitchSystemAsync()
        {
            string varName = nameTable.Entries[TFFSEnvironmentID.LinuxFSStart].Name;
            string value = await GetEnvironmentValueAsync(varName);

            value = value ?? "0";

            if (value.CompareTo("1") != 0 && value.CompareTo("0") != 0) // 'nfs' is treated as an error
            {
                throw new EVAClientException(String.Format("Unexpected value '{0:s}' of '{1:s}' found.", value, varName));
            }

            string newValue = (value.CompareTo("0") == 0) ? "1" : "0";

            await SetEnvironmentValueAsync(varName, newValue);
        }

        public override async Task<MemoryStream> RetrieveStreamAsync(string FileName)
        {
            FTPAction action;
            MemoryStream data = new MemoryStream();

            // set transfer mode
            action = await SetTransferModeAsync();
            await action.AsyncTask;

            // set transfer type
            await SetMediaTypeAsync();

            // set passive connection mode
            action = await SetConnectionModeAsync();
            await action.AsyncTask;

            data = await base.RetrieveStreamAsync(FileName);

            return data;
        }

        public async Task RetrieveFileAsync(string FileName, string SaveAs)
        {
            MemoryStream data = await RetrieveStreamAsync(FileName);
            await data.CopyToAsync(File.Create(SaveAs));
        }

        public async Task<FTPAction> Store(MemoryStream data, string FileName)
        {
            await Task.CompletedTask;

            return null;
        }

        public async Task<FTPAction> Store(string DataFile, string FileName)
        {
            byte[] fileData = File.ReadAllBytes(DataFile);
            MemoryStream data = new MemoryStream(fileData);

            return await Store(data, FileName);
        }

        protected virtual void OnActionCompletedEVA(FTPAction Action)
        {
            EventHandler<ActionCompletedEventArgs> handler = ActionCompleted;
            if (handler != null)
            {
                try
                {
                    handler(this, new ActionCompletedEventArgs(Action));
                }
                catch
                {
                    // no exceptions from event handlers
                }
            }
        }

        private void Initialize()
        {
            base.ActionCompleted += OnActionCompleted;
        }

        private int RemoveFinishedEventHandlerTasks()
        {
            List<Task> finishedHandlers = new List<Task>();

            lock (SyncRoot)
            {
                outstandingEventHandlers.ForEach((task) => { if (task.IsCompleted) finishedHandlers.Add(task); });
                finishedHandlers.ForEach((task) => outstandingEventHandlers.Remove(task));
            }
            return outstandingEventHandlers.Count;
        }

        private async Task SetMediaTypeAsync()
        {
            FTPAction action;
            EVAResponse response;
            EVAResponseValues values;
            EVAMedia media = EVAMediaFactory.GetMedia()[p_MediaType];

            // set media type with thorough check of the result
            action = await RunCommandAndCheckResultAsync(
                String.Format(commands[EVACommandType.MediaType].CommandValue, media.Name),
                responses.FindFlag(EVAResponseFlags.MediaTypeMessage).Code,
                commands[EVACommandType.MediaType]
                );

            await action.AsyncTask;

            if (!action.Success || action.Response == null || (response = responses.FindResponse(action.Response.Code, action.Response.Message)) == null)
            {
                throw new EVAClientException("Unexpected response (no match for result code and message) while setting media type.");
            }

            values = response.Parse(action.Response.Message);
            // if the regular expression fails and we'll get no mediatype value, the previous check fails already (response would be null)
            if (values[response.RegexMatches[0]].CompareTo(media.Response) != 0)
            {
                throw new EVAClientException(String.Format("Unexpected answer ({0:s}) while setting media type.", values[response.RegexMatches[0]]));
            }
        }

        protected override async Task<FTPAction> SetConnectionModeAsync()
        {
            FTPAction action;
            EVAResponse response;
            EVAResponseValues values;

            // set connection mode (to passive, because EVA does not understand active mode with 'PORT' command)
            action = await RunCommandAndCheckResultAsync(
                commands[EVACommandType.Passive].CommandValue,
                responses.FindFlag(EVAResponseFlags.DataConnectionParameters).Code,
                commands[EVACommandType.Passive]
                );

            await action.AsyncTask;

            if (!action.Success || action.Response == null || (response = responses.FindResponse(action.Response.Code, action.Response.Message)) == null)
            {
                throw new EVAClientException(String.Format("Unexpected response (no match for result code and message) while setting connection mode with '{0:s}'.", commands[EVACommandType.Passive].CommandValue));
            }

            values = response.Parse(action.Response.Message);
            string addr = String.Format("{0:s}.{1:s}.{2:s}.{3:s}", values["a1"], values["a2"], values["a3"], values["a4"]);
            int port = (Int32.Parse(values["p1"]) << 8) + Int32.Parse(values["p2"]);
            base.DataPort = port;
            base.DataAddress = IPAddress.Parse(addr);

            return action;
        }

        protected override async Task<FTPAction> SetTransferModeAsync()
        {
            FTPAction action;
            EVAResponse response;
            EVAResponseValues values;
            EVADataModeValue datamode = EVADataModeFactory.GetModes().FindFTPDataType(base.TransferDataType);

            // set transfer type with thorough check of result, overrides the base class method to provide more control
            action = await RunCommandAndCheckResultAsync(
                String.Format(commands[EVACommandType.Type].CommandValue, datamode.Name),
                responses.FindFlag(EVAResponseFlags.TransferTypeMessage).Code,
                commands[EVACommandType.Type]
                );

            await action.AsyncTask;

            if (!action.Success || action.Response == null || (response = responses.FindResponse(action.Response.Code, action.Response.Message)) == null)
            {
                throw new EVAClientException("Unexpected response (no match for result code and message) while setting transfer type.");
            }

            values = response.Parse(action.Response.Message);
            if (values[response.RegexMatches[0]].CompareTo(datamode.Response) != 0)
            {
                throw new EVAClientException(String.Format("Unexpected answer ({0:s}) while setting transfer type.", values[response.RegexMatches[0]]));
            }

            return action;
        }

        private void CheckName(string name, TFFSNameTable table)
        {
            if (table != null && table.FindID(name) == TFFSEnvironmentID.Free)
            {
                throw new EVAClientException(String.Format("Variable name '{0:s}' not found in the specified name table ({1:s}).", name, table.Version));
            }
        }

        private void OnActionCompleted(Object sender, ActionCompletedEventArgs e)
        {
            if (e.Action.Response.Code == notLoggedInCode)
            {
                if (!p_IgnoreLoginError)
                {
                    if (e.Action.UserData != null && ((EVACommand)e.Action.UserData).Equals(commands[EVACommandType.Password]))
                    {
                        e.Action.AsyncTaskCompletionSource.SetException(new EVAClientException("Login failed, wrong password."));
                    }
                    else
                    {
                        e.Action.AsyncTaskCompletionSource.SetException(new EVAClientException("Login needed."));
                    }
                }

                return;
            }
            else if (e.Action.Response.Code == goodbyeCode)
            {
                lock (SyncRoot)
                {
                    p_IsLoggedIn = false;
                }
            }

            FTPAction clone = e.Action.Clone();

            lock (SyncRoot)
            {
                outstandingEventHandlers.Add(Task.Run(() => OnActionCompletedEVA(clone)));
            }
        }
    }

    public class EVADefaults
    {
        // static initial settings
        public readonly static string EVADefaultIP = "192.168.178.1";
        public readonly static int EVADefaultDiscoveryPort = 5035;
        public readonly static string EVADefaultUser = "adam2";
        public readonly static string EVADefaultPassword = "adam2";
        public readonly static int EVADiscoveryTimeout = 120;
    }
}
