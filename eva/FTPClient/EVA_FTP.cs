using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace YourFritz.EVA
{
    public enum EVAMediaType
    {
        // access flash partition
        Flash,
        // access device RAM
        RAM,
    }

    public class EVAMedia
    {
        private EVAMediaType p_Type;
        private string p_Name;
        private string p_Response;

        public EVAMedia(EVAMediaType MediaType, string Name, string Response)
        {
            p_Type = MediaType;
            p_Name = Name;
            p_Response = Response;
        }

        public EVAMediaType MediaType
        {
            get
            {
                return p_Type;
            }
        }

        public string Name
        {
            get
            {
                return p_Name;
            }
        }

        public string Response
        {
            get
            {
                return p_Response;
            }
        }
    }

    public class EVAMediaDictionary : Dictionary<EVAMediaType, EVAMedia>
    {
    }

    public class EVAMediaFactory
    {
        static private EVAMediaDictionary sp_Media;

        public static EVAMediaDictionary GetMedia()
        {
            if (EVAMediaFactory.sp_Media != null)
            {
                return EVAMediaFactory.sp_Media;
            }

            EVAMediaDictionary media = new EVAMediaDictionary();
            EVAMediaFactory.sp_Media = media;

            media.Add(EVAMediaType.Flash, new EVAMedia(EVAMediaType.Flash, "FLSH", "MEDIA_FLASH"));
            media.Add(EVAMediaType.RAM, new EVAMedia(EVAMediaType.Flash, "SDRAM", "MEDIA_SDRAM"));

            return EVAMediaFactory.sp_Media;
        }
    }

    public enum EVADataMode
    {
        // transfer data as ASCII, convert newline characters to host value
        Ascii,
        // transfer data as binary image
        Binary,
    }

    public class EVADataModeValue
    {
        private EVADataMode p_Mode;
        private string p_Name;
        private string p_Response;
        private FTPClient.DataType p_Type = FTPClient.DataType.Binary;

        public EVADataModeValue(EVADataMode DataMode, string Name, string Response, FTPClient.DataType Type)
        {
            p_Mode = DataMode;
            p_Name = Name;
            p_Response = Response;
            p_Type = Type;
        }

        public EVADataModeValue(EVADataMode DataMode, string Name, string Response)
        {
            p_Mode = DataMode;
            p_Name = Name;
            p_Response = Response;
        }

        public EVADataMode DataMode
        {
            get
            {
                return p_Mode;
            }
        }

        public string Name
        {
            get
            {
                return p_Name;
            }
        }

        public string Response
        {
            get
            {
                return p_Response;
            }
        }

        public FTPClient.DataType FTPDataType
        {
            get
            {
                return p_Type;
            }         
        }
    }

    public class EVADataModeValues : Dictionary<EVADataMode, EVADataModeValue>
    {
        public EVADataModeValue FindFTPDataType(FTPClient.DataType Type)
        {
            foreach (EVADataModeValue v in this.Values)
            {
                if (v.FTPDataType == Type)
                {
                    return v;
                }
            }

            return null;
        }
    }

    public class EVADataModeFactory
    {
        static private EVADataModeValues sp_Modes;

        public static EVADataModeValues GetModes()
        {
            if (EVADataModeFactory.sp_Modes != null)
            {
                return EVADataModeFactory.sp_Modes;
            }

            EVADataModeValues modes = new EVADataModeValues();
            EVADataModeFactory.sp_Modes = modes;

            modes.Add(EVADataMode.Ascii, new EVADataModeValue(EVADataMode.Ascii, "A", "ASCII", FTPClient.DataType.Text));
            modes.Add(EVADataMode.Binary, new EVADataModeValue(EVADataMode.Binary, "I", "BINARY"));

            return EVADataModeFactory.sp_Modes;
        }
    }

    public enum EVAFileType
    {
        // TFFS environment
        Environment,
        // counter values from TFFS, if any - sometimes invalid data is returned (e.g. Puma6 devices)
        Counter,
        // device configuration area, created at finalization during manufacturing
        Configuration,
        // codec file - may be a relict from DaVinci devices
        Codec,
    }

    public class EVAFile
    {
        private EVAFileType p_Type;
        private string p_Name;

        public EVAFile(EVAFileType FileType, string Name)
        {
            p_Type = FileType;
            p_Name = Name;
        }

        public EVAFileType FileType
        {
            get
            {
                return p_Type;
            }
        }

        public string Name
        {
            get
            {
                return p_Name;
            }
        }
    }

    public class EVAFiles : List<EVAFile>
    {
    }

    public class EVAFileFactory
    {
        static private EVAFiles sp_Files;

        public static EVAFiles GetFiles()
        {
            if (EVAFileFactory.sp_Files != null)
            {
                return EVAFileFactory.sp_Files;
            }

            EVAFiles files = new EVAFiles();
            EVAFileFactory.sp_Files = files;

            files.Add(new EVAFile(EVAFileType.Environment, "env"));
            files.Add(new EVAFile(EVAFileType.Environment, "env1"));
            files.Add(new EVAFile(EVAFileType.Environment, "env2"));
            files.Add(new EVAFile(EVAFileType.Environment, "env3"));
            files.Add(new EVAFile(EVAFileType.Environment, "env4"));
            files.Add(new EVAFile(EVAFileType.Counter, "count"));
            files.Add(new EVAFile(EVAFileType.Configuration, "CONFIG"));
            files.Add(new EVAFile(EVAFileType.Configuration, "config"));
            files.Add(new EVAFile(EVAFileType.Codec, "codec0"));
            files.Add(new EVAFile(EVAFileType.Codec, "codec1"));

            return EVAFileFactory.sp_Files;
        }
    }

    public enum EVACommandType
    {
        // abort a running data transfer
        Abort,
        // compute CRC value for partition content
        CheckPartition,
        // get environment value
        GetEnvironmentValue,
        // set media type, see EVAMediaTypes
        MediaType,
        // terminate the control connection, logout the user
        Quit,
        // switch to passive transfer mode
        Passive,
        // switch to passive transfer mode, alternative version to fool proxy servers
        Passive_Alt,
        // send password of user
        Password,
        // reboot the device
        Reboot,
        // retrieve the content of a file, see EVAFiles
        Retrieve,
        // set environment value
        SetEnvironmentValue,
        // store data to flash or SDRAM memory
        Store,
        // get system type information
        SystemType,
        // data transfer mode, see EVADataMode
        Type,
        // remove an environment value
        UnsetEnvironmentValue,
        // set the user name for authentication
        User,
    }

    public class EVACommand
    {
        private EVACommandType p_CommandType;
        private string p_CommandValue;

        public EVACommand(EVACommandType CommandType, string CommandValue)
        {
            p_CommandType = CommandType;
            p_CommandValue = CommandValue;
        }

        public EVACommandType CommandType
        {
            get
            {
                return p_CommandType;
            }
        }

        public string CommandValue
        {
            get
            {
                return p_CommandValue;
            }
        }
    }

    public class EVACommands : Dictionary<EVACommandType, EVACommand>
    {
    }

    public class EVACommandFactory
    {
        static private EVACommands sp_Commands;

        public static EVACommands GetCommands()
        {
            if (EVACommandFactory.sp_Commands != null)
            {
                return EVACommandFactory.sp_Commands;
            }

            EVACommands cmds = new EVACommands();
            EVACommandFactory.sp_Commands = cmds;

            cmds.Add(EVACommandType.Abort, new EVACommand(EVACommandType.Abort, "ABOR"));
            cmds.Add(EVACommandType.CheckPartition, new EVACommand(EVACommandType.CheckPartition, "CHECK {0:s}"));
            cmds.Add(EVACommandType.GetEnvironmentValue, new EVACommand(EVACommandType.GetEnvironmentValue, "GETENV {0:s}"));
            cmds.Add(EVACommandType.MediaType, new EVACommand(EVACommandType.MediaType, "MEDIA {0:s}"));
            cmds.Add(EVACommandType.Quit, new EVACommand(EVACommandType.Quit, "QUIT"));
            cmds.Add(EVACommandType.Passive, new EVACommand(EVACommandType.Passive, "PASV"));
            cmds.Add(EVACommandType.Passive_Alt, new EVACommand(EVACommandType.Passive_Alt, "P@SW"));
            cmds.Add(EVACommandType.Password, new EVACommand(EVACommandType.Password, "PASS {0:s}"));
            cmds.Add(EVACommandType.Reboot, new EVACommand(EVACommandType.Reboot, "REBOOT"));
            cmds.Add(EVACommandType.Retrieve, new EVACommand(EVACommandType.Retrieve, "RETR {0:s}"));
            cmds.Add(EVACommandType.SetEnvironmentValue, new EVACommand(EVACommandType.SetEnvironmentValue, "SETENV {0:s} {1:s}"));
            cmds.Add(EVACommandType.Store, new EVACommand(EVACommandType.Store, "STOR {0:s}"));
            cmds.Add(EVACommandType.SystemType, new EVACommand(EVACommandType.SystemType, "SYST"));
            cmds.Add(EVACommandType.Type, new EVACommand(EVACommandType.Type, "TYPE {0:s}"));
            cmds.Add(EVACommandType.UnsetEnvironmentValue, new EVACommand(EVACommandType.UnsetEnvironmentValue, "UNSETENV {0:s}"));
            cmds.Add(EVACommandType.User, new EVACommand(EVACommandType.User, "USER {0:s}"));

            return EVACommandFactory.sp_Commands;
        }
    }

    public enum EVAErrorSeverity
    {
        // no error
        Success,
        // continuation expected
        Continue,
        // temporary failure
        TemporaryFailure,
        // permanent failure, operation not started
        PermanentFailure,
    }

    [Flags]
    public enum EVAResponseFlags
    {
        ClosesConnection = 16,
        ClosesDataConnection = 2,
        DataConnectionParameters = 2048,
        GoodbyeMessage = 8,
        Identity = 1024,
        LoggedIn = 256,
        MediaTypeMessage = 8192,
        None = 0,
        NotImplemented = 32,
        NotLoggedIn = 64,
        PasswordRequired = 128,
        StartsDataConnection = 1,
        TransferTypeMessage = 16384,
        VariableNotSet = 4096,
        WelcomeMessage = 4,
        WrongMultiLineResponse = 512,
    }

    public class EVAResponseValues : Dictionary<string, string>
    {
    }

    public class EVAResponse
    {
        private EVAErrorSeverity p_Severity = EVAErrorSeverity.PermanentFailure;
        private int p_Code;
        private string p_Message = String.Empty;
        private string p_RegexMask = String.Empty;
        private string[] p_RegexMatches = new string[0];
        private Regex p_Match;
        private EVAResponseFlags p_Flags = EVAResponseFlags.None;

        public EVAResponse(int Code, EVAResponseFlags Flags, string Message, EVAErrorSeverity Severity, string RegexMask, string[] RegexMatches)
        {
            p_Code = Code;
            p_Flags = Flags;
            p_Message = Message;
            p_Severity = Severity;
            p_RegexMask = RegexMask;
            p_RegexMatches = RegexMatches;
            p_Match = new Regex(p_RegexMask, RegexOptions.Compiled & RegexOptions.CultureInvariant & RegexOptions.ExplicitCapture);
        }

        public EVAResponse(int Code, EVAResponseFlags Flags, string Message, EVAErrorSeverity Severity)
        {
            p_Code = Code;
            p_Flags = Flags;
            p_Message = Message;
            p_Severity = Severity;
        }

        public EVAResponse(int Code, EVAResponseFlags Flags, string Message, string RegexMask, string[] RegexMatches)
        {
            p_Code = Code;
            p_Flags = Flags;
            p_Message = Message;
            p_RegexMask = RegexMask;
            p_RegexMatches = RegexMatches;
            p_Match = new Regex(p_RegexMask, RegexOptions.Compiled & RegexOptions.CultureInvariant & RegexOptions.ExplicitCapture);
        }

        public EVAResponse(int Code, EVAResponseFlags Flags, string Message)
        {
            p_Code = Code;
            p_Flags = Flags;
            p_Message = Message;
        }

        public EVAResponse(int Code, EVAResponseFlags Flags, EVAErrorSeverity Severity, string RegexMask, string[] RegexMatches)
        {
            p_Code = Code;
            p_Flags = Flags;
            p_Severity = Severity;
            p_RegexMask = RegexMask;
            p_RegexMatches = RegexMatches;
            p_Match = new Regex(p_RegexMask, RegexOptions.Compiled & RegexOptions.CultureInvariant & RegexOptions.ExplicitCapture);
        }

        public EVAResponse(int Code, EVAResponseFlags Flags, EVAErrorSeverity Severity)
        {
            p_Code = Code;
            p_Flags = Flags;
            p_Severity = Severity;
        }

        public EVAResponse(int Code)
        {
            p_Code = Code;
        }

        public int Code
        {
            get
            {
                return p_Code;
            }
        }

        public string Message
        {
            get
            {
                return p_Message;
            }
        }

        public EVAErrorSeverity Severity
        {
            get
            {
                return p_Severity;
            }
        }

        public string RegexMask
        {
            get
            {
                return p_RegexMask;
            }
        }

        public string[] RegexMatches
        {
            get
            {
                return p_RegexMatches;
            }
        }

        public Regex Regex
        {
            get
            {
                return p_Match;
            }
        }

        public EVAResponseFlags Flags
        {
            get
            {
                return p_Flags;
            }
        }

        public EVAResponseValues Parse(string Response)
        {
            EVAResponseValues output = new EVAResponseValues();
            Match matches = p_Match.Match(Response);

            foreach (string name in p_RegexMatches)
            {
                int groupNumber = p_Match.GroupNumberFromName(name);
                Group g = matches.Groups[p_Match.GroupNumberFromName(name)];
                if (g != null)
                {
                    output.Add(name, g.Value);
                }
            }

            return output;
        }
    }

    public class EVAResponses : List<EVAResponse>
    {
        public List<EVAResponse> GetResponses(int Code)
        {
            List<EVAResponse> output = new List<EVAResponse>();

            ForEach((r) =>
            {
                if (r.Code == Code)
                {
                    output.Add(r);
                }
            });

            return output;
        }

        public EVAResponse FindResponse(int Code, string Message)
        {
            foreach(EVAResponse r in this)
            {
                if (r.Code == Code)
                {
                    if (r.Regex == null)
                    {
                        if (r.Message.CompareTo(Message) == 0)
                        {
                            return r;
                        }
                    }
                    else
                    {
                        Match found = r.Regex.Match(Message);

                        if (found.Success)
                        {
                            return r;
                        }
                    }
                }
            }

            return null;
        }

        public EVAResponse FindFlag(EVAResponseFlags Flags)
        {
            foreach (EVAResponse r in this)
            {
                if (r.Flags == Flags)
                {
                    return r;
                }
            }

            return null;
        }

        public int CodeForSuccess
        {
            get
            {
                return 200;
            }
        }
    }

    public class EVAResponseFactory
    {
        static private EVAResponses sp_Responses;

        private EVAResponseFactory() { }

        public static EVAResponses GetResponses()
        {
            if (EVAResponseFactory.sp_Responses != null)
            {
                return EVAResponseFactory.sp_Responses;
            }

            EVAResponses resp = new EVAResponses();
            EVAResponseFactory.sp_Responses = resp;

            resp.Add(new EVAResponse(120, EVAResponseFlags.None, @"Service not ready, please wait", EVAErrorSeverity.TemporaryFailure));
            resp.Add(new EVAResponse(150, EVAResponseFlags.StartsDataConnection, @"Opening {0:s} data connection", EVAErrorSeverity.Success, @"^Opening (?<mode>[ASCII\|BINARY]) data connection$", new string[] { "mode" }));
            resp.Add(new EVAResponse(150, EVAResponseFlags.None, @"Flash check 0x{0:x}", EVAErrorSeverity.Success, @"^Flash check 0x(?<value>[0-9a-fA-F]*)$", new string[] { "value" }));
            resp.Add(new EVAResponse(200, EVAResponseFlags.WrongMultiLineResponse, @"GETENV command successful", EVAErrorSeverity.Success));
            resp.Add(new EVAResponse(200, EVAResponseFlags.None, @"SETENV command successful", EVAErrorSeverity.Success));
            resp.Add(new EVAResponse(200, EVAResponseFlags.None, @"UNSETENV command successful", EVAErrorSeverity.Success));
            resp.Add(new EVAResponse(200, EVAResponseFlags.MediaTypeMessage, @"Media set to {0:s}", EVAErrorSeverity.Success, @"^Media set to (?<mediatype>.*)$", new string[] { "mediatype" }));
            resp.Add(new EVAResponse(200, EVAResponseFlags.TransferTypeMessage, @"Type set to {0:s}", EVAErrorSeverity.Success, @"^Type set to (?<type>.*)$", new string[] { "type" }));
            resp.Add(new EVAResponse(215,  EVAResponseFlags.Identity, @"AVM EVA Version {0:d}.{1:s} 0x{2:x} 0x{3:x}{4:s}", EVAErrorSeverity.Success, @"^AVM EVA Version (?<version>[^ ]*) .*$", new string[] { "version" }));
            resp.Add(new EVAResponse(220,  EVAResponseFlags.WelcomeMessage, @"ADAM2 FTP Server ready", EVAErrorSeverity.Success));
            resp.Add(new EVAResponse(221,  EVAResponseFlags.GoodbyeMessage, @"Goodbye", EVAErrorSeverity.Success));
            resp.Add(new EVAResponse(221,  EVAResponseFlags.GoodbyeMessage, @"Thank you for using the FTP service on ADAM2", EVAErrorSeverity.Success));
            resp.Add(new EVAResponse(226,  EVAResponseFlags.ClosesDataConnection, @"Transfer complete", EVAErrorSeverity.Success));
            resp.Add(new EVAResponse(227,  EVAResponseFlags.DataConnectionParameters, @"Entering Passive Mode ({0:d},{1:d},{2:d},{3:d},{4:d},{5:d})", EVAErrorSeverity.Success, @"^Entering Passive Mode \((?<a1>[0-9]{1,3}),(?<a2>[0-9]{1,3}),(?<a3>[0-9]{1,3}),(?<a4>[0-9]{1,3}),(?<p1>[0-9]{1,3}),(?<p2>[0-9]{1,3})\)$", new string[] { "a1", "a2", "a3", "a4", "p1", "p2" }));
            resp.Add(new EVAResponse(230,  EVAResponseFlags.LoggedIn, @"User {0:s} successfully logged in", EVAErrorSeverity.Success, @"^User (?<user>.*) successfully logged in$", new string[] { "user" }));
            resp.Add(new EVAResponse(331,  EVAResponseFlags.PasswordRequired, @"Password required for {0:s}", EVAErrorSeverity.Continue, @"^Password required for (?<user>.*)$", new string[] { "user" }));
            resp.Add(new EVAResponse(425, EVAResponseFlags.None, @"can'nt open data connection"));
            resp.Add(new EVAResponse(426,  EVAResponseFlags.ClosesDataConnection, @"Data connection closed"));
            resp.Add(new EVAResponse(501,  EVAResponseFlags.VariableNotSet, @"environment variable not set"));
            resp.Add(new EVAResponse(501, EVAResponseFlags.None, @"unknown variable {0:s}", EVAErrorSeverity.PermanentFailure, @"^unknown variable (?<var>.*)$", new string[] { "var" }));
            resp.Add(new EVAResponse(501,  EVAResponseFlags.ClosesDataConnection, @"store failed"));
            resp.Add(new EVAResponse(501, EVAResponseFlags.None, @"Syntax error: Invalid number of parameters"));
            resp.Add(new EVAResponse(502,  EVAResponseFlags.NotImplemented, @"Command not implemented"));
            resp.Add(new EVAResponse(505, EVAResponseFlags.None, @"Close Data connection first"));
            resp.Add(new EVAResponse(530,  EVAResponseFlags.NotLoggedIn, @"not logged in"));
            resp.Add(new EVAResponse(551, EVAResponseFlags.None, @"unknown Mediatype"));
            resp.Add(new EVAResponse(553, EVAResponseFlags.None, @"Urlader_Update failed."));
            resp.Add(new EVAResponse(553, EVAResponseFlags.None, @"Flash erase failed."));
            resp.Add(new EVAResponse(553,  EVAResponseFlags.ClosesDataConnection, @"RETR failed."));
            resp.Add(new EVAResponse(553,  EVAResponseFlags.ClosesDataConnection, @"Execution failed."));

            return EVAResponseFactory.sp_Responses;
        }
    }
}
