using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YourFritz.TFFS;

/// This code uses older syntax from before C# 6, to be usable even from PowerShell 5.1 with .NET Framework.
/// With C# 6 and above, much new features may be used ... e.g. lambda expressions as method bodies or nullable
/// types for event handlers.

namespace YourFritz.EVA
{
    public class FTPClientException : Exception
    {
        internal FTPClientException()
        {
        }

        internal FTPClientException(string message)
            : base(message)
        {
        }

        internal FTPClientException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    public class CommandSentEventArgs : EventArgs
    {
        private readonly string p_Line = String.Empty;
        private readonly DateTime p_SentAt = DateTime.Now;

        internal CommandSentEventArgs(string Line)
        {
            p_Line = Line;
        }

        public string Line
        {
            get
            {
                return p_Line;
            }
        }

        public DateTime SentAt
        {
            get
            {
                return p_SentAt;
            }
        }
    }

    public class ResponseReceivedEventArgs : EventArgs
    {
        private readonly string p_Line = String.Empty;
        private readonly DateTime p_ReceivedAt = DateTime.Now;

        internal ResponseReceivedEventArgs(string Line)
        {
            p_Line = Line;
        }

        public string Line
        {
            get
            {
                return p_Line;
            }
        }

        public DateTime ReceivedAt
        {
            get
            {
                return p_ReceivedAt;
            }
        }
    }

    public class ResponseCompletedEventArgs : EventArgs
    {
        private readonly FTPResponse p_Response;
        private readonly DateTime p_CompletedAt = DateTime.Now;

        internal ResponseCompletedEventArgs(FTPResponse Response)
        {
            p_Response = Response;
        }

        public FTPResponse Response
        {
            get
            {
                return p_Response;
            }
        }

        public DateTime CompletedAt
        {
            get
            {
                return p_CompletedAt;
            }
        }
    }

    public class ActionCompletedEventArgs : EventArgs
    {
        private readonly FTPAction p_Action;
        private readonly DateTime p_FinishedAt = DateTime.Now;

        internal ActionCompletedEventArgs(FTPAction Action)
        {
            p_Action = Action;
        }

        public FTPAction Action
        {
            get
            {
                return p_Action;
            }
        }

        public DateTime FinishedAt
        {
            get
            {
                return p_FinishedAt;
            }
        }
    }

    public class DataConnectionOpenedEventArgs : EventArgs
    {
        private readonly DateTime p_OpenedAt = DateTime.Now;

        internal DataConnectionOpenedEventArgs()
        {
        }

        public DateTime OpenedAt
        {
            get
            {
                return p_OpenedAt;
            }
        }
    }

    public class DataReceivedEventArgs : EventArgs
    {
        private readonly byte[] p_Buffer;
        private readonly int p_Size;
        private bool p_Cancel = false;
        private readonly DateTime p_ReceivedAt = DateTime.Now;

        internal DataReceivedEventArgs(byte[] Data, int Size)
        {
            p_Buffer = Data;
            p_Size = Size;
        }

        public byte[] Data
        {
            get
            {
                return p_Buffer;
            }
        }

        public int Size
        {
            get
            {
                return p_Size;
            }
        }

        public bool Cancel
        {
            get
            {
                return p_Cancel;
            }
            set
            {
                p_Cancel = value;
            }
        }

        public DateTime ReceivedAt
        {
            get
            {
                return p_ReceivedAt;
            }
        }
    }

    public class DataReceiveFailureEventArgs : EventArgs
    {
        private readonly Exception p_Exception;
        private bool p_Cancel = false;
        private readonly DateTime p_FailedAt = DateTime.Now;

        internal DataReceiveFailureEventArgs(Exception Exception)
        {
            p_Exception = Exception;
        }

        public Exception Exception
        {
            get
            {
                return p_Exception;
            }
        }

        public bool Cancel
        {
            get
            {
                return p_Cancel;
            }
            set
            {
                p_Cancel = value;
            }
        }

        public DateTime FailedAt
        {
            get
            {
                return p_FailedAt;
            }
        }
    }

    public class FTPResponse
    {
        private volatile TaskCompletionSource<FTPResponse> p_Task = new TaskCompletionSource<FTPResponse>(TaskCreationOptions.AttachedToParent);
        private int p_Code = -1;
        private string p_Message = String.Empty;
        private bool p_IsComplete = false;
        protected object p_SyncRoot = new object();

        internal FTPResponse()
        {
        }

        internal FTPResponse(int Code)
        {
            p_Code = Code;
        }

        internal FTPResponse(int Code, string Message)
        {
            p_Code = Code;
            p_Message = Message;
        }

        internal FTPResponse(FTPResponse source)
        {
            p_Code = source.Code;
            p_Message = source.Message;
            p_IsComplete = source.IsComplete;
        }

        public bool IsComplete
        {
            get
            {
                return p_IsComplete;
            }
        }

        public virtual bool IsSingleLine
        {
            get
            {
                return true;
            }
        }

        public int Code
        {
            get
            {
                return p_Code;
            }
            internal set
            {
                p_Code = value;
            }
        }

        public string Message
        {
            get
            {
                return p_Message;
            }
        }

        internal TaskCompletionSource<FTPResponse> AsyncTaskCompletionSource
        {
            get
            {
                return p_Task;
            }
        }

        public virtual FTPResponse Clone()
        {
            if (this is MultiLineResponse)
            {
                return ((MultiLineResponse)this).Clone();
            }
            else
            {
                return new FTPResponse(this);
            }
        }

        public override string ToString()
        {
            return String.Format("{0:d} {1:s}", p_Code, p_Message);
        }

        internal void SetCompletion()
        {
            p_IsComplete = true;
            p_Task.SetResult(this);
        }

        internal void SetCompletion(Exception e)
        {
            p_IsComplete = true;
            p_Task.SetException(e);
        }
    }

    public class MultiLineResponse : FTPResponse
    {
        private List<string> p_Content = new List<string>();
        private string p_InitialMessage = String.Empty;
        private string p_FinalMessage = String.Empty;

        internal MultiLineResponse() : base()
        {
        }

        internal MultiLineResponse(string FirstContentLine) : base()
        {
            AppendLine(FirstContentLine);
        }

        internal MultiLineResponse(string InitialMessage, int Code) : base(Code)
        {
            p_InitialMessage = InitialMessage;
        }

        private MultiLineResponse(MultiLineResponse source) : base(source)
        {
            p_Content = new List<string>(source.Content);
            p_InitialMessage = source.InitialMessage;
            p_FinalMessage = source.FinalMessage;
        }

        public override bool IsSingleLine
        {
            get
            {
                return false;
            }
        }

        public List<string> Content
        {
            get
            {
                return p_Content;
            }
        }

        public string InitialMessage
        {
            get
            {
                return p_InitialMessage;
            }
        }

        public string FinalMessage
        {
            get
            {
                return p_FinalMessage;
            }
        }

        internal void AppendLine(string Line)
        {
            lock (p_SyncRoot)
            {
                p_Content.Add(Line);
            }
        }

        internal void Finish(string FinalMessage, int Code)
        {
            if (base.Code != -1 && base.Code != Code)
            {
                base.SetCompletion(new FTPClientException(String.Format("Multi-line response was started with code {0:d} and finished with code {1:d} - a violation of RFC 959.", base.Code, Code)));
                return;
            }

            p_FinalMessage = FinalMessage;
            if (base.Code == -1) base.Code = Code;

            base.SetCompletion();
        }

        public override FTPResponse Clone()
        {
            return new MultiLineResponse(this);
        }

        public override string ToString()
        {
            return String.Format("{0:d} {1:s}", base.Code, p_FinalMessage);
        }
    }

    public class FTPAction
    {
        private string p_Command = String.Empty;
        private volatile TaskCompletionSource<FTPAction> p_Task = new TaskCompletionSource<FTPAction>(TaskCreationOptions.AttachedToParent);
        private FTPResponse p_Response = new FTPResponse();
        private bool p_Aborted = false;
        private bool p_Success = false;
        private bool p_Completed = false;
        private int p_ExpectedAnswer = 0;
        private readonly object p_Lock = new object();
        private object p_UserData = null;

        internal FTPAction()
        {
        }

        internal FTPAction(string Command)
        {
            p_Command = Command;
        }

        internal FTPAction(string Command, object UserData)
        {
            p_Command = Command;
            p_UserData = UserData;
        }

        internal FTPAction(string Command, int ExpectedCode)
        {
            p_Command = Command;
            p_ExpectedAnswer = ExpectedCode;
        }

        internal FTPAction(string Command, int ExpectedCode, object UserData)
        {
            p_Command = Command;
            p_ExpectedAnswer = ExpectedCode;
            p_UserData = UserData;
        }

        private FTPAction(FTPAction source)
        {
            p_Command = source.Command;
            p_Response = source.Response.Clone();
            p_Aborted = source.Aborted;
            p_Success = source.Success;
            p_ExpectedAnswer = source.ExpectedAnswer;
            p_UserData = source.UserData;
        }

        public string Command
        {
            get
            {
                return p_Command;
            }
        }

        public FTPResponse Response
        {
            get
            {
                return p_Response;
            }
            internal set
            {
                p_Response = value;
            }
        }

        public bool Completed
        {
            get
            {
                return p_Completed;
            }
        }

        public bool Success
        {
            get
            {
                if (!p_Success)
                {
                    CheckSuccess();
                }
                return p_Success;
            }
        }

        public int ExpectedAnswer
        {
            get
            {
                return p_ExpectedAnswer;
            }
            internal set
            {
                p_ExpectedAnswer = value;
            }
        }

        public object UserData
        {
            get
            {
                return p_UserData;
            }
        }

        internal Task<FTPAction> AsyncTask
        {
            get
            {
                return p_Task.Task;
            }
        }

        internal TaskCompletionSource<FTPAction> AsyncTaskCompletionSource
        {
            get
            {
                return p_Task;
            }
        }

        private bool Aborted
        {
            get
            {
                return p_Aborted;
            }
        }

        public object SyncRoot
        {
            get
            {
                return p_Lock;
            }
        }

        public bool Cancel()
        {
            // TODO: implement abortion of a running command
            lock (SyncRoot)
            {
                if (!p_Response.IsComplete)
                {
                    p_Aborted = true;
                }
            }
            return p_Aborted;
        }

        public FTPAction Clone()
        {
            return new FTPAction(this);
        }

        internal void SetCompletion()
        {
            p_Completed = true;
            if (!p_Task.Task.IsCompleted)
            {
                p_Task.SetResult(this);
            }
        }

        public override string ToString()
        {
            return String.Format("{0:s}: {1:d} {2:s}", p_Command, p_Response.Code, p_Response.Message);
        }

        internal void CheckSuccess()
        {
            if (p_Response.Code == p_ExpectedAnswer)
            {
                p_Success = true;
            }
        }
    }

    internal class FTPControlChannelReceiver
    {
        private Regex p_MatchResponse = new Regex(@"^(?<code>\d{3})(?<delimiter>[ \t-])(?<message>.*)$", RegexOptions.Compiled);
        private Queue<FTPResponse> p_ResponseQueue = new Queue<FTPResponse>();
        private FTPResponse p_CurrentResponse = null;
        private bool p_CloseNow = false;
        private readonly object p_Lock = new object();

        public EventHandler<ResponseReceivedEventArgs> ResponseReceived;
        public EventHandler<ResponseCompletedEventArgs> ResponseCompleted;

        internal FTPControlChannelReceiver()
        {
        }

        internal Queue<FTPResponse> Responses
        {
            get
            {
                return p_ResponseQueue;
            }
        }

        internal bool CloseNow
        {
            get
            {
                return p_CloseNow;
            }
        }

        internal object SyncRoot
        {
            get
            {
                return p_Lock;
            }
        }

        internal async Task AddResponse(string Line)
        {
            MatchCollection matches = p_MatchResponse.Matches(Line);

            await OnResponseReceived(Line);

            if (matches.Count > 0)
            {
                int code = -1;
                bool startMultiline = false;
                string message = String.Empty;

                code = Convert.ToInt32(matches[0].Groups[1].Value);
                startMultiline = matches[0].Groups[2].Value.CompareTo("-") == 0;
                message = matches[0].Groups[3].Value;

                if (startMultiline)
                {
                    if (p_CurrentResponse != null && !p_CurrentResponse.IsSingleLine)
                    {
                        p_CurrentResponse.AsyncTaskCompletionSource.SetException(new FTPClientException(String.Format("Multi-line start message with code {0:d} (see RFC 959) after previous response lines.", code)));
                        return;
                    }
                    p_CurrentResponse = new MultiLineResponse(Line, code);
                }
                else
                {
                    if (p_CurrentResponse != null)
                    {
                        ((MultiLineResponse)p_CurrentResponse).Finish(message, code);
                    }
                    else
                    {
                        p_CurrentResponse = new FTPResponse(code, message);
                    }

                    FTPResponse completed = p_CurrentResponse;
                    p_CurrentResponse = null;
                    lock (p_Lock)
                    {
                        p_ResponseQueue.Enqueue(completed);
                    }

                    await OnResponseCompleted(completed);
                }
            }
            else
            {
                if (p_CurrentResponse != null)
                {
                    ((MultiLineResponse)p_CurrentResponse).AppendLine(Line);
                }
                else
                {
                    p_CurrentResponse = new MultiLineResponse(Line);
                }
            }
        }

        internal void Clear()
        {
            lock (p_Lock)
            {
                p_ResponseQueue.Clear();
                p_CurrentResponse = null;
            }
        }

        internal void SetException(Exception e)
        {
            FTPResponse response = new FTPResponse(FTPClient.genericErrorCode[0], "Client error");
            response.AsyncTaskCompletionSource.SetException(e);
            lock (p_Lock)
            {
                p_ResponseQueue.Enqueue(response);
            }
        }

        internal void Close()
        {
            lock (SyncRoot)
            {
                p_CloseNow = true;
            }
        }

        protected async Task OnResponseReceived(string Line)
        {
            await Task.CompletedTask;

            EventHandler<ResponseReceivedEventArgs> handler = ResponseReceived;
            if (handler != null)
            {
                try
                {
                    handler(this, new ResponseReceivedEventArgs(Line));
                }
                catch
                {
                    // no exceptions from event handlers
                }
            }

            await Task.CompletedTask;
        }

        protected async Task OnResponseCompleted(FTPResponse Response)
        {
            await Task.CompletedTask;

            EventHandler<ResponseCompletedEventArgs> handler = ResponseCompleted;
            try
            {
                if (handler != null)
                {
                    handler(this, new ResponseCompletedEventArgs(Response));
                }
                if (!Response.AsyncTaskCompletionSource.Task.IsCompleted)
                {
                    Response.AsyncTaskCompletionSource.SetResult(Response);
                }
            }
            catch (Exception e)
            {
                if (!Response.AsyncTaskCompletionSource.Task.IsCompleted)
                {
                    Response.AsyncTaskCompletionSource.SetException(e);
                }
            }

            await Task.CompletedTask;
        }
    }

    // generic FTP client class
    public class FTPClient
    {
        // generic FTP status codes for state machine, overwrite them, if your server uses different values than EVA
        internal static int[] openedDataConnectionCode = { 150 };
        internal static int[] closedDataConnectionCode = { 226, 426 };
        internal static int[] openedControlConnectionCode = { 220 };
        internal static int[] closedControlConnectionCode = { 221, 421 };
        internal static int[] genericErrorCode = { 500 };

        public enum DataConnectionMode
        {
            Active = 1,
            Passive = 2,
        }

        public enum DataConnectionDirection
        {
            Write = 1,
            Read = 2,
        }

        public enum DataType
        {
            Text = 1,
            Binary = 2,
        }

        public event EventHandler<CommandSentEventArgs> CommandSent;
        public event EventHandler<ResponseReceivedEventArgs> ResponseReceived;
        public event EventHandler<ActionCompletedEventArgs> ActionCompleted;
        public event EventHandler<DataConnectionOpenedEventArgs> DataConnectionOpened;
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<DataReceiveFailureEventArgs> DataReceiveFailure;

        // properties fields
        private IPAddress p_Address = IPAddress.Any;
        private int p_Port = 21;
        private DataConnectionMode p_DataConnectionMode = DataConnectionMode.Passive;
        private string[] p_PassiveConnectionCommands = new string[] { "PASV", "P@SW" };
        private string p_PassiveConnectionCommand = "PASV";
        private string p_PassiveConnectionCommandParseAnswer = @"^227 .*\(([0-9]{1,3},?){6}\)$";
        private DataType p_DataType = DataType.Binary;
        private string p_DataTypeImageCommand = "TYPE I";
        private string p_DataTypeAsciiCommand = "TYPE A";
        private IPAddress p_DataAddress;
        private int p_DataPort = 22;
        private string p_RetrieveCommand = "RETR {0:s}";
        private int p_ConnectTimeout = 120000;
        private bool p_IsOpened = false;
        private bool p_OpenedDataConnection = false;
        private bool p_DataConnectionAborted = false;
        private bool p_ForciblyClosed = false;
        private string p_AbortTransferCommand = "ABOR";
        private FTPAction p_CurrentAction = null;
        protected readonly object p_Lock = new object();

        // solely private values, never exposed to others
        private TcpClient controlConnection = null;
        private StreamWriter controlWriter = null;
        private StreamReader controlReader = null;
        private FTPControlChannelReceiver controlChannelReceiver = null;
        private Task controlChannelReaderTask = null;
        private DataConnectionDirection direction = DataConnectionDirection.Read;
        private MemoryStream readDataStream = null;
        private int dataRead = 0;
        private CancellationTokenSource dataChannelReadCancellationTokenSource = null;
        private Task dataChannelReadingTask = null;
        private Task dataChannelWritingTask = null;

        public FTPClient()
        {
        }

        public FTPClient(string Address)
        {
            p_Address = IPAddress.Parse(Address);
            p_DataAddress = p_Address;
        }

        public FTPClient(string Address, int Port)
        {
            p_Address = IPAddress.Parse(Address);
            p_DataAddress = p_Address;
            this.Port = Port;
        }

        public FTPClient(IPAddress Address)
        {
            p_Address = Address;
            p_DataAddress = p_Address;
        }

        public FTPClient(IPAddress Address, int Port)
        {
            p_Address = Address;
            p_DataAddress = p_Address;
            this.Port = Port;
        }

        public IPAddress Address
        {
            get
            {
                return p_Address;
            }
        }

        public int Port
        {
            get
            {
                return p_Port;
            }
            internal set
            {
                if (value > 65534 || value < 1)
                {
                    throw new FTPClientException(String.Format("Invalid port number {0:s} specified.", Convert.ToString(value)));
                }
                if (IsOpen)
                {
                    throw new FTPClientException("Port may only be set on a closed connection.");
                }
                p_Port = value;
            }
        }

        public DataConnectionMode ConnectionMode
        {
            get
            {
                return p_DataConnectionMode;
            }
            set
            {
                if (value != DataConnectionMode.Passive)
                {
                    throw new FTPClientException("Only passive transfer mode is implemented yet.");
                }

                p_DataConnectionMode = value;
            }
        }

        protected string[] PassiveConnectionCommands
        {
            get
            {
                return p_PassiveConnectionCommands;
            }
            set
            {
                p_PassiveConnectionCommands = value;
            }
        }

        protected string PassiveConnectionCommand
        {
            get
            {
                return p_PassiveConnectionCommand;
            }
            set
            {
                bool found = false;

                Array.ForEach(p_PassiveConnectionCommands, (c) =>
                {
                    if (c.CompareTo(value) == 0)
                    {
                        found = true;
                    }
                });

                if (!found)
                {
                    throw new FTPClientException(String.Format("The specified command '{0:s}' is not supported.", value));
                }

                p_PassiveConnectionCommand = value;
            }
        }

        protected string PassiveConnectionCommandAnswerMask
        {
            get
            {
                return p_PassiveConnectionCommandParseAnswer;
            }
            set
            {
                p_PassiveConnectionCommandParseAnswer = value;
            }
        }

        protected string AbortTransferCommand
        {
            get
            {
                return p_AbortTransferCommand;
            }
            set
            {
                p_AbortTransferCommand = value;
            }
        }

        public DataType TransferDataType
        {
            get
            {
                return p_DataType;
            }
            set
            {
                if (value != DataType.Binary)
                {
                    throw new FTPClientException("Only binary transfers are supported yet.");
                }
                p_DataType = value;
            }
        }

        protected string TransferTypeImageCommand
        {
            get
            {
                return p_DataTypeImageCommand;
            }
            set
            {
                p_DataTypeImageCommand = value;
            }
        }

        protected string TransferTypeAsciiCommand
        {
            get
            {
                return p_DataTypeAsciiCommand;
            }
            set
            {
                p_DataTypeAsciiCommand = value;
            }
        }

        public int DataPort
        {
            get
            {
                return p_DataPort;
            }
            set
            {
                if (HasOpenDataConnection)
                {
                    throw new FTPClientException("Data port may only be set, while no data connection is open.");
                }
                p_DataPort = value;
            }
        }

        public IPAddress DataAddress
        {
            get
            {
                return p_DataAddress;
            }
            set
            {
                if (HasOpenDataConnection)
                {
                    throw new FTPClientException("Data address may only be set, while no data connection is open.");
                }
                p_DataAddress = value;
            }
        }

        protected string RetrieveCommand
        {
            get
            {
                return p_RetrieveCommand;
            }
            set
            {
                p_RetrieveCommand = value;
            }
        }

        public int ConnectTimeout
        {
            get
            {
                return p_ConnectTimeout;
            }
            set
            {
                if (IsOpen)
                {
                    throw new FTPClientException("Connect timeout may only be set on a closed connection.");
                }
                p_ConnectTimeout = value;
            }
        }

        public bool IsOpen
        {
            get
            {
                bool opened;

                lock (SyncRoot)
                {
                    opened = p_IsOpened;
                }

                return opened;
            }
        }

        public bool IsClosedByServer
        {
            get
            {
                return p_ForciblyClosed;
            }
        }

        public bool IsConnected
        {
            get
            {
                if (IsOpen)
                {
                    return controlConnection.Connected;
                }
                return false;
            }
        }

        public bool HasOpenDataConnection
        {
            get
            {
                return p_OpenedDataConnection;
            }
        }

        public FTPAction CurrentAction
        {
            get
            {
                return p_CurrentAction;
            }
        }

        internal virtual object SyncRoot
        {
            get
            {
                return p_Lock;
            }
        }

        public virtual async Task OpenAsync()
        {
            await OpenAsync(p_Address, p_Port);
        }

        public virtual async Task OpenAsync(string Address)
        {
            await OpenAsync(IPAddress.Parse(Address), p_Port);
        }

        public virtual async Task OpenAsync(string Address, int Port)
        {
            await OpenAsync(IPAddress.Parse(Address), Port);
        }

        public virtual async Task OpenAsync(IPAddress Address)
        {
            await OpenAsync(Address, p_Port);
        }

        public virtual async Task OpenAsync(IPAddress Address, int Port)
        {
            if (IsOpen)
            {
                throw new FTPClientException("The connection is already opened.");
            }
            p_Address = Address;
            p_Port = Port;

            try
            {
                controlConnection = new TcpClient(p_Address.ToString(), p_Port);
            }
            catch (SocketException e)
            {
                throw new FTPClientException("Error connecting to FTP server.", e);
            }

            controlWriter = await OpenWriter(controlConnection);
            controlChannelReceiver = new FTPControlChannelReceiver();
            controlChannelReceiver.ResponseReceived += OnControlChannelResponseReceived;
            controlChannelReceiver.ResponseCompleted += OnControlChannelResponseCompleted;
            controlReader = await OpenReader(controlConnection);

            controlChannelReaderTask = Task.Run(async () => { await AsyncControlReader(controlReader, controlChannelReceiver); });

            Task waitForConnection = Task.Run(async () => { while (!IsOpen) await Task.Delay(10); });

            await Task.CompletedTask;

            if (!waitForConnection.Wait(p_ConnectTimeout)) throw new FTPClientException(String.Format("Timeout connecting to FTP server at {0:s}:{1:d}.", p_Address, p_Port));
        }

        public async virtual Task CloseAsync()
        {
            bool waitForCompletion = false;

            if (p_CurrentAction != null && !p_CurrentAction.AsyncTask.IsCompleted)
            {
                if (p_CurrentAction.Cancel())
                {
                    FTPAction abort = new FTPAction(p_AbortTransferCommand);
                    await WriteCommandAsync(abort);
                    waitForCompletion = true;
                }
            }

            if (waitForCompletion)
            {
                try
                {
                    await p_CurrentAction.AsyncTask;
                }
                catch
                {
                    // ignore any exception during close
                }
            }

            await Task.CompletedTask;

            lock (SyncRoot)
            {
                p_IsOpened = false;
            }

            if (controlChannelReceiver != null)
            {
                controlChannelReceiver.Close();
                Task.WaitAny(new Task[] { controlChannelReaderTask }, 1000);
            }

            if (controlConnection != null)
            {
                try
                {
                    controlConnection.Close();
                }
                catch (ObjectDisposedException)
                { }
                catch (InvalidOperationException)
                { }
            }

            if (controlChannelReceiver != null)
            {
                await controlChannelReaderTask;
            }
        }

        public FTPResponse NextResponse()
        {
            FTPResponse nextResponse = null;

            lock (p_Lock)
            {
                if (controlChannelReceiver.Responses.Count > 0)
                {
                    nextResponse = controlChannelReceiver.Responses.Dequeue();
                }
            }

            return nextResponse;
        }

        public void ClearResponses()
        {
            controlChannelReceiver.Clear();
        }

        public async Task<FTPAction> StartActionAsync(string Command)
        {
            return await StartActionAsync(Command, null);
        }

        public async Task<FTPAction> StartActionAsync(string Command, object UserData)
        {
            if (!IsOpen)
            {
                throw new FTPClientException("Unable to issue a command on a closed client connection.");
            }

            if (Command.CompareTo(p_AbortTransferCommand) == 0)
            {
                if (p_CurrentAction == null)
                {
                    throw new FTPClientException("There is no command in progress, which could get aborted.");
                }
            }
            else
            {
                if (p_CurrentAction != null)
                {
                    throw new FTPClientException("There is already a command in progress.");
                }
                // remove any garbage from previous command, if the new one is not an ABOR command
                controlChannelReceiver.Clear();
            }

            FTPAction newAction = new FTPAction(Command, UserData);
            lock (SyncRoot)
            {
                p_CurrentAction = newAction;
            }

            await OnCommandSent(newAction.Command);

            //Debug.WriteLine(String.Format("before WriteCommandAsync: CurrentAction = {0}", p_CurrentAction.ToString()));
            await WriteCommandAsync(newAction);
            //if (p_CurrentAction != null)
            //{
            //    Debug.WriteLine(String.Format("after WriteCommandAsync: CurrentAction = {0}", p_CurrentAction.ToString()));
            //}
            //else
            //{
            //    Debug.WriteLine(String.Format("after WriteCommandAsync: CurrentAction is null, command was {0}", Command));
            //}

            return newAction;
        }

        public async virtual Task<FTPAction> RunCommandAsync(string Command)
        {
            return await RunCommandAsync(Command, null);
        }

        public async virtual Task<FTPAction> RunCommandAsync(string Command, object UserData)
        {
            Task<FTPAction> commandTask = StartActionAsync(Command, UserData);
            FTPAction action = null;

            try
            {
                action = await commandTask;
            }
            catch (AggregateException e)
            {
                //Debug.WriteLine("AggregateException");
                // unbox aggregated exceptions
                foreach (Exception ie in e.InnerExceptions)
                {
                    //Debug.WriteLine(String.Format("AggregateException with {0}: {1}", ie.ToString(), ie.StackTrace));
                    throw ie;
                }
            }

            //if (action != null)
            //{
            //    Debug.WriteLine(String.Format("Action={0}", action.ToString()));
            //}
            //else
            //{
            //    Debug.WriteLine("Action is null");
            //}

            return action;
        }

        private async Task WriteCommandAsync(FTPAction action)
        {
            try
            {
                await controlWriter.WriteLineAsync(action.Command);
            }
            catch (Exception e)
            {
                if (!IsOpen && (e is ObjectDisposedException))
                {
                    action.AsyncTaskCompletionSource.SetException(new FTPClientException("The connection was closed by the server."));
                }
                else
                {
                    action.AsyncTaskCompletionSource.SetException(e);
                }
            }
        }

        protected virtual async Task<FTPAction> SetTransferModeAsync()
        {
            string command = (p_DataType == DataType.Binary) ? p_DataTypeImageCommand : p_DataTypeAsciiCommand;

            return await StartActionAsync(command, null);
        }

        protected virtual async Task<FTPAction> SetConnectionModeAsync()
        {
            await Task.CompletedTask;

            throw new FTPClientException("Method SetConnectionModeAsync() not implemented yet on class FTPClient.");
        }

        public virtual async Task<MemoryStream> RetrieveStreamAsync(string FileName)
        {
            direction = DataConnectionDirection.Read;
            readDataStream = new MemoryStream();

            TcpClient dataConnection = new TcpClient(p_DataAddress.ToString(), p_DataPort)
            {
                LingerState = new LingerOption(true, 1),
                NoDelay = true,
                // use only small buffers to force packet size to stay below "normal" Ethernet limits (no jumbo packets)
                // this gives us better control over the process of sending or receiving data, too
                ReceiveBufferSize = 1024,
                SendBufferSize = 1024
            };

            dataChannelReadCancellationTokenSource = new CancellationTokenSource();
            dataChannelReadingTask = ReceiveAsync(dataConnection, readDataStream, dataChannelReadCancellationTokenSource);

            FTPAction action = await StartActionAsync(String.Format(p_RetrieveCommand, FileName), null);
            await action.AsyncTask;

            dataConnection.Close();

            MemoryStream stream = readDataStream;
            stream.Seek(0, SeekOrigin.Begin);
            readDataStream = null;

            dataChannelReadCancellationTokenSource.Dispose();
            dataChannelReadCancellationTokenSource = null;
            dataChannelReadingTask = null;

            return stream;
        }

        protected async Task OnDataConnectionOpened(FTPResponse Response)
        {
            lock (SyncRoot)
            {
                p_OpenedDataConnection = true;
            }

            EventHandler<DataConnectionOpenedEventArgs> handler = DataConnectionOpened;
            if (handler != null)
            {
                try
                {
                    handler(this, new DataConnectionOpenedEventArgs());
                }
                catch
                {
                    // ignore exceptions from event handlers
                }
            }

            if (direction == DataConnectionDirection.Read)
            {
                await dataChannelReadingTask;
            }
            else
            {
                await dataChannelWritingTask;
            }
        }

        protected async Task OnDataCommandCompleted(FTPResponse Response)
        {
            lock (SyncRoot)
            {
                p_OpenedDataConnection = false;
            }

            await Task.CompletedTask;
        }

        protected async virtual Task<bool> OnDataReceived(byte[] Buffer, int BytesRead)
        {
            bool cancelReceiving = false;

            await Task.CompletedTask;

            EventHandler<DataReceivedEventArgs> handler = DataReceived;
            if (handler != null)
            {
                try
                {
                    DataReceivedEventArgs ev = new DataReceivedEventArgs(Buffer, BytesRead);

                    handler(this, ev);

                    cancelReceiving = ev.Cancel;
                }
                catch
                {
                    // ignore exceptions from event handlers
                }
            }

            return cancelReceiving;
        }

        protected async virtual Task<bool> OnDataReceivedException(Exception e)
        {
            bool cancelReceiving = false;

            await Task.CompletedTask;

            EventHandler<DataReceiveFailureEventArgs> handler = DataReceiveFailure;
            if (handler != null)
            {
                try
                {
                    DataReceiveFailureEventArgs ev = new DataReceiveFailureEventArgs(e);

                    handler(this, ev);

                    cancelReceiving = ev.Cancel;
                }
                catch
                {
                    // ignore exceptions from event handlers
                }
            }

            return cancelReceiving;
        }

        private async Task ReceiveAsync(TcpClient connection, MemoryStream output, CancellationTokenSource ctSource)
        {
            byte[] buffer = new byte[1024];
            bool finished = false;

            dataRead = 0;

            while (!finished && !ctSource.IsCancellationRequested)
            {
                try
                {
                    int read = await connection.GetStream().ReadAsync(buffer, 0, buffer.Length, ctSource.Token);

                    if (read > 0)
                    {
                        output.Write(buffer, 0, read);
                        dataRead += read;
                        if (await OnDataReceived(buffer, read))
                        {
                            finished = true; // data transfer finished by event handler
                        }
                    }
                    else
                    {
                        finished = true; // EOF at zero bytes read
                    }
                }
                catch (ObjectDisposedException e)
                {
                    if (e.Source == "System.Net.Sockets.NetworkStream") // stream closed after control message
                    {
                        finished = true;
                    }
                }
                catch (Exception e)
                {
                    if (await OnDataReceivedException(e))
                    {
                        if (!ctSource.IsCancellationRequested)
                        {
                            ctSource.Cancel();
                        }
                    }
                }
            }
        }

        protected async virtual Task OnCommandSent(string Line)
        {
            await Task.CompletedTask;

            EventHandler<CommandSentEventArgs> handler = CommandSent;
            if (handler != null)
            {
                try
                {
                    handler(this, new CommandSentEventArgs(Line));
                }
                catch
                {
                    // ignore exceptions from event handlers
                }
            }

            await Task.CompletedTask;
        }

        protected async virtual Task OnResponseReceived(ResponseReceivedEventArgs e)
        {
            await Task.CompletedTask;

            EventHandler<ResponseReceivedEventArgs> handler = ResponseReceived;
            if (handler != null)
            {
                try
                {
                    handler(this, e);
                }
                catch
                {
                    // ignore exceptions from event handlers
                }
            }

            await Task.CompletedTask;
        }

        protected async virtual Task OnActionCompleted(FTPAction Action)
        {
            await Task.CompletedTask;

            EventHandler<ActionCompletedEventArgs> handler = ActionCompleted;
            try
            {
                if (handler != null)
                {
                    handler(this, new ActionCompletedEventArgs(Action));
                }

                if (!Action.AsyncTask.IsCompleted)
                {
                    Action.AsyncTaskCompletionSource.SetResult(Action);
                }
            }
            catch (Exception e)
            {
                if (!Action.AsyncTask.IsCompleted)
                {
                    Action.AsyncTaskCompletionSource.SetException(e);
                }
            }
        }

        private async Task<StreamWriter> OpenWriter(TcpClient connection)
        {
            StreamWriter writer = new StreamWriter(connection.GetStream(), Encoding.ASCII)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };
            await Task.CompletedTask;
            return writer;
        }

        private async Task<StreamReader> OpenReader(TcpClient connection)
        {
            await Task.CompletedTask;
            return new StreamReader(connection.GetStream(), Encoding.ASCII);
        }

        private async Task AsyncControlReader(StreamReader reader, FTPControlChannelReceiver receiver)
        {
            do
            {
                bool empty = false;

                try
                {
                    string readLine = await reader.ReadLineAsync();
                    if (readLine != null)
                    {
                        empty = false;
                        await receiver.AddResponse(readLine);
                    }
                    else
                    {
                        empty = true;
                    }
                }
                catch (Exception e)
                {
                    receiver.SetException(e);
                    break;
                }

                if (empty) await Task.Delay(10);
            } while (!receiver.CloseNow);

            reader.Close();
        }

        private async Task OnControlCommandCompleted(FTPResponse Response)
        {
            FTPAction lastAction = null;

            lock (SyncRoot)
            {
                if (p_CurrentAction != null)
                {
                    lastAction = p_CurrentAction;
                }
            }

            if (lastAction != null)
            {
                lock (SyncRoot)
                {
                    p_CurrentAction = null;
                }

                //Debug.WriteLine(String.Format("OnControlCommandCompleted: Response was {0}, last action = {1}", Response.ToString(), lastAction.ToString()));
                lastAction.Response = Response;

                await OnActionCompleted(lastAction);

                lastAction.SetCompletion();
            }
            //else
            //{
            //    Debug.WriteLine(String.Format("Response was {0}, last action is null", Response.ToString()));
            //}
        }

        private async void OnControlChannelResponseReceived(Object sender, ResponseReceivedEventArgs e)
        {
            await OnResponseReceived(e);
        }

        private async void OnControlChannelResponseCompleted(Object sender, ResponseCompletedEventArgs e)
        {
            bool handled = false;
            bool completed = true;

            if (!handled)
            {
                foreach (int code in FTPClient.openedDataConnectionCode)
                {
                    if (e.Response.Code == code)
                    {
                        await OnDataConnectionOpened(e.Response);
                        handled = true;
                        completed = false;
                        break;
                    }
                }
            }

            if (!handled)
            {
                foreach (int code in FTPClient.closedDataConnectionCode)
                {
                    if (e.Response.Code == code)
                    {
                        await OnDataCommandCompleted(e.Response);
                        handled = true;
                        break;
                    }
                }
            }

            if (!handled)
            {
                foreach (int code in FTPClient.openedControlConnectionCode)
                {
                    if (e.Response.Code == code)
                    {
                        lock (SyncRoot)
                        {
                            p_IsOpened = true;
                        }
                        handled = true;
                        break;
                    }
                }
            }

            if (!handled)
            {
                foreach (int code in FTPClient.closedControlConnectionCode)
                {
                    if (e.Response.Code == code)
                    {
                        lock (SyncRoot)
                        {
                            p_IsOpened = false;
                            if (code >= 400)
                            {
                                p_ForciblyClosed = true;
                            }
                        }
                        controlConnection.Close();
                        handled = true;
                        break;
                    }
                }
            }

            if (completed)
            {
                await OnControlCommandCompleted(e.Response);
            }
        }
    }
}
