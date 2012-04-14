//------------------------------------------------------------------------------
//
//    Copyright 2012, Marc Meijer
//
//    This file is part of RaidarGadget.
//
//    RaidarGadget is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    RaidarGadget is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with RaidarGadget. If not, see <http://www.gnu.org/licenses/>.
//
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;

namespace RaidarGadget {

    /// <summary>
    /// Implements a simple static circular log buffer that can be dumped to
    /// file when, for instance, an error occurs. The log file created will
    /// have an unique timstamped filename.
    /// </summary>
    public static class CircularLogBuffer {

        private static List<string> buffer;
        private static int length = 10;
        private static int head;
        private static long entryNumber;

        /// <summary>
        /// Constructor
        /// </summary>
        static CircularLogBuffer() {
            buffer = new List<string>();
        }

        /// <summary>
        /// Adds a new log entry to the buffer
        /// </summary>
        /// <param name="entry"></param>
        public static void Add(string entry) {
            entryNumber++;
            DateTime now = DateTime.Now;
            string nowString = String.Format("[{0}-{1:D2}-{2:D2} {3:D2}:{4:D2}:{5:D2}.{6:D3}]",
                now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, now.Millisecond);
            string logEntry = entryNumber + " " + nowString + " " + entry + "\n\n";

            // Add entry
            if (buffer.Count >= length) {
                buffer[head] = logEntry;
            } else {
                buffer.Add(logEntry);
            }

            // Update the head
            head++;
            if (head == length) {
                head = 0;
            }
        }

        /// <summary>
        /// Adds a new exception log entry to buffer
        /// </summary>
        /// <param name="exception"></param>
        public static void Add(Exception exception) {
            string exceptionMessage = string.Format(
                "Exception: " + exception.GetType() +
                " Message: " + exception.Message + "\n"
                + exception.StackTrace);
            Add(exceptionMessage);
        }

        /// <summary>
        /// Get the buffer as a list of strings
        /// </summary>
        /// <returns></returns>
        public static List<string> Get() {
            List<string> tempBuffer = new List<string>();

            // Add earliest log entries directly after the head entry
            if ((buffer.Count == length) && (head < (length - 1))) {
                for (int i = 0; i < (length - head); i++) {
                    tempBuffer.Add(buffer[head + i]);
                }
            }

            // add most recent items up to the head
            for (int i = 0; i < head; i++) {
                tempBuffer.Add(buffer[i]);
            }

            return tempBuffer;
        }

        /// <summary>
        /// Dump the log buffer to file with on additional message
        /// </summary>
        /// <param name="additionalMessage"></param>
        public static void DumpLog(string additionalMessage) {
            string nowString = GetNowString();
            string tempPath = System.IO.Path.GetTempPath();
            StreamWriter tempLog = File.CreateText(tempPath + @"\RaidarLog" + nowString + ".log");

            if (tempLog != null) {
                try {
                    List<string> logEntries = CircularLogBuffer.Get();
                    for (int i = 0; i < logEntries.Count; i++) {
                        tempLog.Write(logEntries[i]);
                    }
                    if (!String.IsNullOrEmpty(additionalMessage)) {
                        tempLog.WriteLine(additionalMessage);
                    }
                } finally {
                    tempLog.Flush();
                    tempLog.Close();
                }
            }
        }

        /// <summary>
        /// Dump the log buffer to file
        /// </summary>
        public static void DumpLog() {
            DumpLog(String.Empty);
        }

        /// <summary>
        /// Get unique string based on the date and time
        /// </summary>
        /// <returns></returns>
        private static string GetNowString() {
            DateTime now = DateTime.Now;
            string nowString = String.Format("{0}{1:D2}{2:D2}{3:D2}{4:D2}{5:D2}{6:D3}",
                now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, now.Millisecond);
            return nowString;
        }

    }
}
