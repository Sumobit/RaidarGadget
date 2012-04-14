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
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RaidarGadget {

    /// <summary>
    /// EventArgs specialization containing a NAS status message
    /// </summary>
    public class MessageEventArgs : EventArgs {
        private string msg;

        public MessageEventArgs(string messageData) {
            msg = messageData;
        }

        public string Message {
            get { return msg; }
            set { msg = value; }
        }
    }

    public enum SnmpState {
        /// <summary>
        /// The SNMP client has not been initialized yet.
        /// </summary>
        UnInitialized,
        /// <summary>
        /// Initial state where the SNMP client is still looking for NAS devices.
        /// </summary>
        DiscoveringDevices,
        /// <summary>
        /// The SNMP client is ready to send status requests to the NAS.
        /// </summary>
        Ready,
        /// <summary>
        /// The SNMP client is waiting for a response from the NAS.
        /// </summary>
        WaitingForStatusResponse,
        /// <summary>
        /// The connection with the NAS is lost, the SNMP client will attempt to reconnect
        /// </summary>
        NasConnectionLost,
        /// <summary>
        /// The SNMP client is waiting for a reconnection attempt with the NAS.
        /// </summary>
        WaitingForReconnect
    }

    /// <summary>
    /// Class responsible for handling snmp communication with Netgear ReadyNAS
    /// devices trough the Raidar protocol.
    /// </summary>
    public class RaidarSnmp : IDisposable {

        private const int localRaidarSnmpPort = 0;
        private const int remoteRaidarSnmpPort = 22081;
        private const int udpTimeOut = 30 * 1000; // 1000 equals ~1 sec
        private const int minStatusMsgLength = 100;
        private TimeSpan discoverSearchTime = new TimeSpan(0, 2, 0);
        private bool disposed;
        private IPAddress localIPAddress = IPAddress.None;

        /// <summary>
        /// The payload to send over UDP to the NAS to get SNMP information.
        /// This packet was determined by packet sniffing Raidar with
        /// Wireshark. This is the exact binary content Raidar sends
        /// to the NAS.
        /// Packet data from Raidar version 4.3.2
        /// </summary>
        private byte[] payLoad432 = { 
                                 0x00, 0x00, 0x05, 0xd3,
                                 0x00, 0x00, 0x00, 0x01,
                                 0x00, 0x00, 0x00, 0x00,
                                 0x80, 0xc9, 0x6c, 0x05,
                                 0xff, 0xff, 0xff, 0xff,
                                 0x00, 0x00, 0x00, 0x1c,
                                 0x00, 0x00, 0x00, 0x00
                             };

        /// <summary>
        /// Packet data from Raidar version 4.3.3 and later
        /// </summary>
        private byte[] payLoad433 = { 
                                 0x00, 0x00, 0x05, 0xad,
                                 0x00, 0x00, 0x00, 0x01,
                                 0x00, 0x00, 0x00, 0x00,
                                 0xcb, 0x14, 0x60, 0x55,
                                 0xff, 0xff, 0xff, 0xff,
                                 0x00, 0x00, 0x00, 0x1c,
                                 0x00, 0x00, 0x00, 0x00
                             };

        private UdpClient client;
        private SnmpState state = SnmpState.UnInitialized;

        List<IPAddress> discoveredNasDevices = new List<IPAddress>();

        public event EventHandler<MessageEventArgs> DeviceDiscovered;
        public event EventHandler<MessageEventArgs> StatusMessageReceived;
        public event EventHandler<MessageEventArgs> NasConnectionLost;

        /// <summary>
        /// Constructor
        /// </summary>
        public RaidarSnmp() {
            // Configure UDP client
            client = new UdpClient(localRaidarSnmpPort);
            client.EnableBroadcast = true;
            client.Client.ReceiveTimeout = udpTimeOut;

            // Get local IP
            localIPAddress = GetLocalMachineIPv4Address();
        }

        public void Dispose() {
            if (!disposed) {
                client.Close();
                disposed = true;
            }
            // This object will be cleaned up by the Dispose method. Therefore call GC.SupressFinalize
            // to take this object off the finalization queue and prevent finalization code for this
            // object from executing a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the current state of the connection
        /// </summary>
        public SnmpState State {
            get {
                return state;
            }
        }

        /// <summary>
        /// Asynchronously discovers all ReadyNas devices on the local network
        /// by broadcast of SNMP package over UDP.
        /// </summary>
        public void DiscoverNasDevices() {
            // Create broadcast endpoint
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, remoteRaidarSnmpPort);

            // Initiate the task that will receive and process the broadcast responses
            Debug.WriteLine("Initiate parallel discovery task");
            state = SnmpState.DiscoveringDevices;
            Task discoverTask = new Task(DiscoverTask);
            discoverTask.Start();

            // Send broadcast payload to local network
            Debug.WriteLine("Send broadcast for NAS discovery:");
            client.BeginSend(payLoad433, payLoad433.Length, endPoint, null, null);
        }

        /// <summary>
        /// Task that will handle the incoming UDP messages that resulted from
        /// the broadcast.
        /// </summary>
        private void DiscoverTask() {
            try {
                // Create broadcast endpoint
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, remoteRaidarSnmpPort);

                DateTime now = DateTime.Now;
                DateTime searchEndTime = now + discoverSearchTime;
                Debug.WriteLine("Started DiscoverTask at: " + now);

                // Multiple devices may respond so set up a while loop
                while (DateTime.Now < searchEndTime) {
                    // Receive response from NAS
                    byte[] receivedBytes = client.Receive(ref endPoint);
                    string receivedString = Encoding.ASCII.GetString(receivedBytes);

                    // Filter out own IP address
                    if (!localIPAddress.Equals(endPoint.Address) && !discoveredNasDevices.Contains(endPoint.Address)) {
                        discoveredNasDevices.Add(endPoint.Address);
                        Debug.WriteLine("Discovered network device at: " + endPoint.Address + " ; notifying clients");
                        if (DeviceDiscovered != null) {
                            if (receivedString.Length > minStatusMsgLength) {
                                RaidarGadget.App.Current.Dispatcher.BeginInvoke(
                                    DeviceDiscovered, this, new MessageEventArgs(receivedString));
                            } else {
                                RaidarGadget.App.Current.Dispatcher.BeginInvoke(
                                    DeviceDiscovered, this, new MessageEventArgs(String.Empty));
                            }
                        }
                    }
                }
            } catch (SocketException ex) {
                Debug.WriteLine("SocketException: " + ex.ErrorCode + " Message: " +
                    ex.Message + "\n" + ex.StackTrace);

                // A time out is expected here after one device is discovered so report only
                // SocketException that are not time outs.
                if (ex.SocketErrorCode != SocketError.TimedOut) {
                    CircularLogBuffer.Add(ex);
                    CircularLogBuffer.DumpLog();
                }
            } finally {
                // Always set the state to ready when a NAS was discovered
                if (discoveredNasDevices.Count > 0) {
                    state = SnmpState.Ready;
                }
            }
        }

        /// <summary>
        /// Asynchronously request the status of one NAS device.
        /// </summary>
        public void RequestNasStatus() {
            if ((discoveredNasDevices == null) ||
                ((state != SnmpState.Ready) && (state != SnmpState.NasConnectionLost))
            ) {
                return;
            }

            // Create destination endpoint to first discovered NAS
            IPEndPoint endPoint = new IPEndPoint(discoveredNasDevices[0], remoteRaidarSnmpPort);

            // Initiate the task that will receive and process the NAS response
            Debug.WriteLine("Initiate parallel receive task");
            if (state == SnmpState.Ready) {
                state = SnmpState.WaitingForStatusResponse;
            } else {
                state = SnmpState.WaitingForReconnect;
            }
            Task parallelReceiveTask = new Task(ReceiveTask);
            parallelReceiveTask.Start();

            // Send payload to specific device
            Debug.WriteLine("Send request for NAS SNMP information:");
            client.BeginSend(payLoad433, payLoad433.Length, endPoint, null, null);
        }

        /// <summary>
        /// Task that will handle the incoming UDP message o the device.
        /// </summary>
        private void ReceiveTask() {
            try {
                // Create destination endpoint
                IPEndPoint endPoint = new IPEndPoint(discoveredNasDevices[0], remoteRaidarSnmpPort);

                Debug.WriteLine("Started ReceiveTask at: " + DateTime.Now);

                // Receive response from NAS
                byte[] receivedBytes = client.Receive(ref endPoint);
                Debug.WriteLine("Received ReadyNAS status message");

                // Process response from NAS. Only a correct status message can be parsed.
                string receivedString = Encoding.ASCII.GetString(receivedBytes);

                if (StatusMessageReceived != null && receivedString.Length > minStatusMsgLength) {
                    Debug.WriteLine("Status message is valid; notifying clients");
                    RaidarGadget.App.Current.Dispatcher.BeginInvoke(
                        StatusMessageReceived, this, new MessageEventArgs(receivedString));
                }
                // Reset the state
                state = SnmpState.Ready;
            } catch (SocketException ex) {
                Debug.WriteLine("SocketException: " + ex.ErrorCode + " Message: " +
                    ex.Message + "\n" + ex.StackTrace);

                if (ex.SocketErrorCode == SocketError.TimedOut) {
                    // Destination is unreachable
                    if (state != SnmpState.WaitingForReconnect) {
                        state = SnmpState.NasConnectionLost;
                        // Notify clients only when this state transition happens.
                        if (NasConnectionLost != null) {
                            RaidarGadget.App.Current.Dispatcher.BeginInvoke(
                                NasConnectionLost, this, new MessageEventArgs(ex.SocketErrorCode.ToString()));
                        }
                    } else {
                        state = SnmpState.NasConnectionLost;
                    }
                } else {
                    CircularLogBuffer.Add(ex);
                    CircularLogBuffer.DumpLog();
                    // Reset the state so we can try again next time
                    state = SnmpState.Ready;
                }
            }
        }

        /// <summary>
        /// Gets the IP address of the current machine
        /// </summary>
        /// <returns></returns>
        private static IPAddress GetLocalMachineIPv4Address() {
            IPAddress localIP = IPAddress.None;
            string host = Dns.GetHostName();
            IPHostEntry ip = Dns.GetHostEntry(host);
            if (ip.AddressList != null && ip.AddressList.Length > 0) {
                foreach (IPAddress address in ip.AddressList) {
                    if (address.AddressFamily == AddressFamily.InterNetwork) {
                        localIP = address;
                        break;
                    }
                }
            }
            return localIP;
        }

        /// <summary>
        /// Gets the IP address of a machine by hostname
        /// </summary>
        /// <param name="hostName"></param>
        /// <returns></returns>
        private static IPAddress GetHostNameIPAddress(string hostName) {
            IPAddress ipAddress = IPAddress.None;
            IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
            if (hostEntry.AddressList != null && hostEntry.AddressList.Length > 0) {
                ipAddress = hostEntry.AddressList[0];
            }
            return ipAddress;
        }

    }
}
