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
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace RaidarGadget {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        /// <summary>
        /// Dumps unhandled exceptions to the logfile before the application exits
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendFormat("An unhandled Exception occured on thread ID = " + e.Dispatcher.Thread.ManagedThreadId +
                "\n"+"{0}\n", e.Exception.Message);
            stringBuilder.AppendFormat("{0}\n", e.Exception.StackTrace);

            if (e.Exception.InnerException != null) {
                stringBuilder.AppendFormat("\nInner exception:\n{0}\n", e.Exception.InnerException.Message);
                stringBuilder.AppendFormat(
                        "Exception occured on thread ID {0}.\n", e.Dispatcher.Thread.ManagedThreadId);
                stringBuilder.AppendFormat("{0}\n", e.Exception.InnerException.StackTrace);
            }

            // attempt to save data
            CircularLogBuffer.DumpLog(stringBuilder.ToString());

            Console.WriteLine(stringBuilder);
        }

    }
}
