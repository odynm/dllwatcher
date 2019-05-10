using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace DLLWatcher
{
    class Watcher
    {
        struct Error
        {
            public int index;
            public string source;
            public string destination;
            public string error;

            public Error(int index, string source, string destination, string error)
            {
                this.index = index;
                this.source = source;
                this.destination = destination;
                this.error = error;
            }
        }

        static int frequency = 2000;
        static string[] sources, destinations;
        static DateTime timeLastUpdate;
        static int numPendingMoves;
        static List<Error> pendingErrors;
        static List<int> pendingMoves;
        static bool firstIteration = true;

        static void Main(string[] args)
        {
            File.Open("config.txt", FileMode.Append).Close(); // create if not exist
            string[] configLines = File.ReadAllLines("config.txt");

            // Prealocate arrays
            sources = new string[configLines.Length];
            destinations = new string[configLines.Length];

            pendingErrors = new List<Error>();
            pendingMoves = new List<int>();

            for (int i = 0; i < configLines.Length; i++)
            {
                if (string.IsNullOrEmpty(configLines[i])) continue; // allow white lines
                if (configLines[i][0] == '_') continue; // allow comment in form of "_" starting lines

                int separatorIndex = configLines[i].IndexOf('>');
                sources[i] = configLines[i].Substring(0, separatorIndex).Trim();
                destinations[i] = configLines[i].Substring(separatorIndex+1).Trim();
            }

            // Clear empties caused by white lines
            sources = sources.Where(x => !string.IsNullOrEmpty(x)).ToArray();
            destinations = destinations.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            PrintInfo();
            UpdateTime();
            while (true)
            {
                //
                // Try to move files that have changed since last loop
                //
                for(int i = 0; i < sources.Length; i++)
                {
                    if (File.Exists(sources[i]))
                    {
                        if (firstIteration || File.GetLastWriteTime(sources[i]) > timeLastUpdate)
                        {   // Is it the first iteration? | OR | Does the file have changed since last iteration?
                            try
                            {
                                File.Copy(sources[i], destinations[i], true);
                                PrintCopiedNotice(sources[i].Substring(sources[i].LastIndexOf('\\')));
                            }
                            catch (IOException ex)
                            {   // If failed, log the error and set as a pending move
                                numPendingMoves++;

                                var error = new Error(i, sources[i], destinations[i], ex.Message);
                                pendingErrors.Add(error);
                                pendingMoves.Add(i);
                            }
                        }
                    }
                }

                //
                // Try to resolve files that have failed
                //
                for (int i = pendingMoves.Count - 1; i >= 0; i--)
                { 
                    if (File.Exists(sources[pendingMoves[i]]))
                    {
                        try
                        {
                            File.Copy(sources[pendingMoves[i]], destinations[pendingMoves[i]], true);
                            PrintCopiedNotice(sources[i].Substring(sources[i].LastIndexOf('\\')));

                            // If moved, remove errors and pending moves
                            pendingErrors.Remove(pendingErrors.Find(x => x.index == pendingMoves[i]));
                            pendingMoves.Remove(pendingMoves.Find(x => x == pendingMoves[i]));
                            numPendingMoves--;
                        }
                        catch (IOException)
                        {   // We know that there's a error. We'll try again on the next iteration
                        }
                    }
                }

                firstIteration = false;

                UpdateTime();
                PrintInfo();
                Thread.Sleep(frequency);
            }
        }

        static void PrintInfo()
        {
            Console.WriteLine("=========================");
            Console.WriteLine("Watching DLLs listed on config.txt");
            Console.WriteLine("config.txt example line:");
            Console.WriteLine("C:\\dir\\name.dll > C:\\other\\name.dll");

            if (numPendingMoves > 0)
            {
                Console.WriteLine("");
                Console.WriteLine("ERROR: " + numPendingMoves + " pending move(s):\n");
                foreach (var error in pendingErrors)
                {
                    Console.WriteLine(error.source);
                    Console.WriteLine("to");
                    Console.WriteLine(error.destination);
                    Console.WriteLine("Error: " + error.error);
                    Console.WriteLine("");
                }
            }
            else
            {
                Console.WriteLine("Done for " + sources.Length + " files");
            }
            Console.WriteLine("=========================");
        }

        static void PrintCopiedNotice(string file)
        {
            Console.WriteLine(" ===> Copied " + file);
        }

        static void UpdateTime()
        {
            timeLastUpdate = DateTime.Now;
        }
    }
}
