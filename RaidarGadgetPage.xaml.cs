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
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StringResources = RaidarGadget.Properties.Resources;
using System.Windows.Interop;

namespace RaidarGadget {
    /// <summary>
    /// Defines the interaction logic for the <see cref="Page"/> that is the GUI
    /// of the gadget.
    /// </summary>
    public partial class RaidarGadgetPage : Page {

        // Constants
        private const int maxTemperatures = 4;
        private const int maxFans = 4;
        private const int maxVolumes = 1;
        private const int maxDisks = 8;

        // Statics
        private static SolidColorBrush defaultBrush = new SolidColorBrush(Colors.GhostWhite);
        private static SolidColorBrush alertBrush = new SolidColorBrush(Colors.Red);
        private static DispatcherTimer updateTimer;

        // Globals
        private RaidInfo raidInfo;
        private RaidarSnmp raidar;
        private int pollNasRate = 6;    // Sets Nas polling rate
        private int counter = 0;
        private float oldTemperatureY = 50f;
        private StringAnimationUsingKeyFrames stringAnimation;

        private List<Label> squareLeds = new List<Label>();
        private List<Label> volumeInfoLabels = new List<Label>();

        private bool debug = false;

        public RaidarGadgetPage() {
            Dispatcher.ShutdownStarted += Dispatcher_ShutdownStarted;
            InitializeComponent();

            StartSearchAnimation();

            // Used to easily access the disk leds objects by index later on
            squareLeds.Add(led1square);
            squareLeds.Add(led2square);
            squareLeds.Add(led3square);
            squareLeds.Add(led4square);
            squareLeds.Add(led5square);
            squareLeds.Add(led6square);
            squareLeds.Add(led7square);
            squareLeds.Add(led8square);

            volumeInfoLabels.Add(volumeLabel0);
            volumeInfoLabels.Add(volumeLabel1);     // these might not be needed
            volumeInfoLabels.Add(volumeLabel2);     // these might not be needed

            raidInfo = new RaidInfo();
            raidar = new RaidarSnmp();
            raidar.DeviceDiscovered += OnDeviceDiscovered;
            raidar.StatusMessageReceived += OnNasStatusReceived;
            raidar.NasConnectionLost += OnNasConnectionLost;

            if (NetworkInterface.GetIsNetworkAvailable()) {
                raidar.DiscoverNasDevices();
            } else {
                nasIdLabel.Content = StringResources.String_NoNetwork;
            }
        }

        /// <summary>
        /// Once a NAS is discovered, initiate polling of the nas.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDeviceDiscovered(object sender, MessageEventArgs args) {
            StopSearchAnimation();
            // Perform the initial update
            if (!String.IsNullOrEmpty(args.Message)) {
                UpdateNasStatus(args.Message);
            }

            // Start polling the NAS at regular intervals
            StartPollingNas();
        }

        /// <summary>
        /// Initiates the NAS status polling on a predefined interval
        /// </summary>
        private void StartPollingNas() {
            updateTimer = new DispatcherTimer();
            updateTimer.Interval = new TimeSpan(0, 0, pollNasRate); // 5 sec
            updateTimer.Tick += new EventHandler(updateTimerTick);
            updateTimer.Start();
        }

        /// <summary>
        /// Requests the NAS status at timer interval
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void updateTimerTick(object sender, EventArgs e) {
            RequestNasStatus();
        }
        /// <summary>
        /// Pauses the polling.
        /// </summary>
        private void StopPolling() {
            updateTimer.Stop();
        }

        /// <summary>
        /// Cleans up the raidar object when shutting down.
        /// </summary>
        private void ExitPolling() {
            StopPolling();
            raidar.Dispose();
        }

        /// <summary>
        /// Starts the search animation
        /// </summary>
        private void StartSearchAnimation() {
            stringAnimation = new StringAnimationUsingKeyFrames();
            stringAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(1500));
            stringAnimation.KeyFrames.Add(
                new DiscreteStringKeyFrame(StringResources.String_Searching + ".  ",
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
            stringAnimation.KeyFrames.Add(
                new DiscreteStringKeyFrame(StringResources.String_Searching + ".. ",
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500))));
            stringAnimation.KeyFrames.Add(
                new DiscreteStringKeyFrame(StringResources.String_Searching + "...",
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000))));
            stringAnimation.RepeatBehavior = RepeatBehavior.Forever;
            nasIdLabel.BeginAnimation(Label.ContentProperty, stringAnimation);
        }

        /// <summary>
        /// Stops the search animation
        /// </summary>
        private void StopSearchAnimation() {
            // Stop by beginning a null animation
            nasIdLabel.BeginAnimation(Label.ContentProperty, null);
        }

        /// <summary>
        /// Asynchronously update the NAS status step 1: Send the request
        /// </summary>
        private void RequestNasStatus() {
            raidar.RequestNasStatus();
        }

        /// <summary>
        /// Asynchronously update the NAS status step 2: Callback that updates
        /// the result on the page
        /// </summary>
        private void OnNasStatusReceived(object sender, MessageEventArgs args) {
            UpdateNasStatus(args.Message);
        }

        /// <summary>
        /// Asynchronously update the NAS status step 2: Callback that updates
        /// the result on the page
        /// </summary>
        private void OnNasConnectionLost(object sender, MessageEventArgs args) {
            nasIdLabel.Content = StringResources.String_ConnectionLost;
            SetToolTip(nasIdLabel, args.Message);
            Log(StringResources.String_ConnectionLost + ": " + args.Message);

            SetLabelWithStatus(infoLabel, String.Empty, Status.nas_connection_lost);
            SetToolTip(infoLabel, String.Empty);

            SetLabelWithStatus(upsLabel, String.Empty, Status.nas_connection_lost);
            SetToolTip(upsLabel, String.Empty);

            SetLabelWithStatus(fanSpeedLabel, String.Empty, Status.nas_connection_lost);
            SetToolTip(fanSpeedLabel, String.Empty);
                        
            minTempLabel.Content = String.Empty;
            maxTempLabel.Content = String.Empty;
            SetLabelWithStatus(curTempLabel, String.Empty, Status.nas_connection_lost);

            SetToolTip(curTempLabel, String.Empty);
            //SetToolTip(minTempLabel, String.Empty);
            //SetToolTip(maxTempLabel, String.Empty);
            SetToolTip(thermometerPath, String.Empty);
            SetToolTip(levelPath, String.Empty);

            for (int i = 0; (i < maxVolumes) && (i < raidInfo.Volumes.Count); i++) {
                SetLabelWithStatus(volumeInfoLabels[i], String.Empty, Status.nas_connection_lost);
                SetToolTip(volumeInfoLabels[i], String.Empty);
            }

            for (int i = 0; (i < maxDisks) && i < raidInfo.Disks.Count; i++) {
                SetLedStatus(squareLeds[i], Status.nas_connection_lost);
                squareLeds[i].Content = String.Empty;
                SetToolTip(squareLeds[i], String.Empty);
            }
        }

        /// <summary>
        /// Updates the page with the new NAS status information
        /// </summary>
        /// <param name="result"></param>
        private void UpdateNasStatus(string result) {
            if (!result.Equals(String.Empty) && result.Contains("\t")) {
                Log(result);

                if (debug) {
                    debugLabel.Content = ++counter + " (ok)";
                }

                // Parse the received status message:
                raidInfo.Parse(result);

                SetNasInfo();

                //SetFanInfo();
                if (raidInfo.Fans.Count == 0)
                {
                    // No fan
                    SetFanInfo(null);
                }
                else if (raidInfo.Fans.Count == 1)
                {
                    // only one fan then show details
                    SetFanInfo(raidInfo.Fans[0]);
                }
                else if (raidInfo.Fans.Count >= 1)
                {
                    // sort Fan data by index
                    for (int i = 0; (i < maxFans) && (i < raidInfo.Fans.Count); i++)
                    {
                        for (int j = i + 1; (j < maxFans) && (j < raidInfo.Fans.Count); j++)
                        {
                            if (raidInfo.Fans[i].Index > raidInfo.Fans[j].Index)
                            {
                                RaidFan tempFan = raidInfo.Fans[i];
                                raidInfo.Fans[i] = raidInfo.Fans[j];
                                raidInfo.Fans[j] = tempFan;
                            }
                        }
                    }
                    SetFanInfo(raidInfo.Fans[0]);
                    string txtFan = "";
                    for (int i = 0; (i < maxFans) && (i < raidInfo.Fans.Count); i++)
                    {
                        // set tooltip with summary for all fans
                        txtFan += raidInfo.Fans[i].FanType + " " + raidInfo.Fans[i].FanSpeed +
                            StringResources.String_rpm + " " + raidInfo.Fans[i].Status;
                        if (i < raidInfo.Fans.Count-1)
                        {
                            txtFan += "\n";
                        }
                        SetToolTip(fanSpeedLabel, txtFan);
                    }
                }
                
                SetUpsInfo();

                //SetTemperatureInfo();
                if (raidInfo.Temperatures.Count == 0)
                {
                    // no temp
                    SetTemperatureInfo(null);
                }
                else if (raidInfo.Temperatures.Count == 1)
                {
                    // only one temp then show details
                    SetTemperatureInfo(raidInfo.Temperatures[0]);
                }
                else if (raidInfo.Temperatures.Count >= 1)
                {
                    // sort Temperature data by index
                    for (int i = 0; (i < maxTemperatures) && (i < raidInfo.Temperatures.Count); i++)
                    {
                        for (int j = i + 1; (j < maxTemperatures) && (j < raidInfo.Temperatures.Count); j++)
                        {
                            if (raidInfo.Temperatures[i].Index > raidInfo.Temperatures[j].Index)
                            {
                                RaidTemperature tmpTemp = raidInfo.Temperatures[i];
                                raidInfo.Temperatures[i] = raidInfo.Temperatures[j];
                                raidInfo.Temperatures[j] = tmpTemp;
                            }
                        }
                    }
                    SetTemperatureInfo(raidInfo.Temperatures[0]);
                    string txtTemp = "";
                    for (int i = 0; (i < maxTemperatures) && (i < raidInfo.Temperatures.Count); i++)
                    {
                        // set tooltip with summary for all temperatures (label, thermometer and level)
                        txtTemp += raidInfo.Temperatures[i].Index + " " + raidInfo.Temperatures[i].TempCelcius +
                            StringResources.String_Celsius + " " + raidInfo.Temperatures[i].Status;
                        if (i < raidInfo.Temperatures.Count-1)
                        {
                            txtTemp += "\n";
                        }
                        SetToolTip(curTempLabel, txtTemp);
                        SetToolTip(thermometerPath, txtTemp);
                        SetToolTip(levelPath, txtTemp);
                    }
                }
 
                // set Volume information
                for (int i = 0; (i < maxVolumes) && (i < raidInfo.Volumes.Count); i++) {
                    SetVolumeStatus(raidInfo.Volumes[i], volumeInfoLabels[i]);
                }

                // set Disk information
                for (int i = 0; (i < maxDisks) && i < raidInfo.Disks.Count; i++) {
                    SetDiskStatus(squareLeds[i], raidInfo.Disks[i]);
                }
            }
        }

        /// <summary>
        /// Update the device information
        /// </summary>
        private void SetNasInfo() {
            nasIdLabel.Content = raidInfo.Name;
            SetToolTip(nasIdLabel, String.Empty);

            infoLabel.Content = raidInfo.Model + "\n" + raidInfo.IpAdress;
            SetToolTip(infoLabel,
                StringResources.String_MacAddress + raidInfo.MacAdress.ToUpper() + "\n" +
                StringResources.String_Firmware + raidInfo.SoftwareName + " v" + raidInfo.SoftwareVersion);
        }

        /// <summary>
        /// Update UPS information
        /// </summary>
        private void SetUpsInfo() {
            if ((raidInfo.Ups == null) || (raidInfo.Ups.Status == Status.not_present)) 
            {
                //upsLabel.Content = String.Empty;
                SetLabelWithStatus(upsLabel, String.Empty, Status.unknown);
                upsLabel.Background = new SolidColorBrush(Colors.Transparent);
                upsLabel.BorderBrush = new SolidColorBrush(Colors.Transparent);

                // Clear Tooltip message
                SetToolTip(upsLabel, String.Empty);
            } 
            else 
            {
                // Set label text and alert status
                if (raidInfo.Ups.Status == Status.ok)
                {
                    SetLabelWithStatus(upsLabel, StringResources.String_UPS + raidInfo.Ups.Charge + "%", Status.ok);
                    upsLabel.Background = new SolidColorBrush(Colors.Lime);
                    upsLabel.BorderBrush = new SolidColorBrush(Colors.LimeGreen);
                    upsLabel.Foreground = new SolidColorBrush(Colors.Black);
                }
                else //if (raidInfo.Ups.Status == Status.warn)
                {
                    SetLabelWithStatus(upsLabel, StringResources.String_UPS + raidInfo.Ups.TimeLeft, raidInfo.Ups.Status);
                    upsLabel.Background = new SolidColorBrush(Colors.Orange);
                    upsLabel.BorderBrush = new SolidColorBrush(Colors.DarkOrange);
                }

                // Set Tooltip message
                SetToolTip(upsLabel, StringResources.String_UPS + "\n" + raidInfo.Ups.Description + "\n" +
                    StringResources.String_UPS_Battery + raidInfo.Ups.Charge + "%, " + raidInfo.Ups.TimeLeft +"\n" +
                    RaidStatusMap.GetStatusString(raidInfo.Ups.Status, raidInfo.Ups));
            }
        }
        
        /// <summary>
        /// Update fan information
        /// </summary>
        /// <param name="fan"></param>
        private void SetFanInfo(RaidFan fan)
        {
            if ((fan == null) || (fan.Status == Status.not_present))
            {
                fanSpeedLabel.Content = String.Empty;
                fanSpeedLabel.Background = new SolidColorBrush(Colors.Transparent);
                fanSpeedLabel.BorderBrush = new SolidColorBrush(Colors.Transparent);

                // Clear Tooltip message
                SetToolTip(fanSpeedLabel, String.Empty);
            }
            else
            {
                // Set label text and alert status
                if (fan.Status == Status.ok)
                {
                    SetLabelWithStatus(fanSpeedLabel, StringResources.String_Fan + fan.FanSpeed + StringResources.String_rpm, Status.ok);
                    fanSpeedLabel.Background = new SolidColorBrush(Colors.Lime);
                    fanSpeedLabel.BorderBrush = new SolidColorBrush(Colors.LimeGreen);
                    fanSpeedLabel.Foreground = new SolidColorBrush(Colors.Black);
                }
                else //if (raidInfo.Fan.Status == Status.warn)
                {
                    SetLabelWithStatus(fanSpeedLabel, StringResources.String_Fan + fan.FanSpeed + StringResources.String_rpm, fan.Status);
                    fanSpeedLabel.Background = new SolidColorBrush(Colors.Orange);
                    fanSpeedLabel.BorderBrush = new SolidColorBrush(Colors.DarkOrange);
                }

                // Set Tooltip message
                SetToolTip(fanSpeedLabel, RaidStatusMap.GetStatusString(fan.Status, fan));
            }
        }

        /// <summary>
        /// Update volume statuses
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="label"></param>
        private void SetVolumeStatus(RaidVolume volume, Label label) {
            // Volume label
            float percentUsed = ((float)volume.GbUsed / (float)volume.GbTotal) * 100f;
            label.Content = volume.Name + ": " + percentUsed.ToString("F0") +
                StringResources.String_percent_used;
            // Volume label tooltip
            if (volume.Status == Status.ok) {
                string volumeInfoTooltipString = StringResources.String_Volume +
                    volume.RaidLevel + ", " + volume.RaidStatus + "\n" +
                    volume.GbUsed + StringResources.String_Gb_used_of +
                    volume.GbTotal + StringResources.String_Gb;
                SetToolTip(label, volumeInfoTooltipString);
                label.Foreground = defaultBrush;
            } else {
                SetToolTip(label, RaidStatusMap.GetStatusString(volume.Status, volume));
                label.Foreground = alertBrush;
            }
        }

        /// <summary>
        /// Update temperature information
        /// </summary>
        /// <param name="fan"></param>
        private void SetTemperatureInfo(RaidTemperature temperature)
        {
            if (temperature != null)
            {
                // Set label values
                minTempLabel.Content = temperature.MinExpectedCelcius +
                    StringResources.String_Celsius;
                maxTempLabel.Content = temperature.MaxExpectedCelcius +
                    StringResources.String_Celsius;
                curTempLabel.Content = temperature.TempCelcius +
                    StringResources.String_Celsius;

                // Tooltip and alert status
                SetToolTip(curTempLabel, RaidStatusMap.GetStatusString(
                    temperature.Status, temperature));
                //SetToolTip(minTempLabel, RaidStatusMap.GetStatusString(
                //    raidInfo.Temperature.Status, raidInfo.Temperature));
                //SetToolTip(maxTempLabel, RaidStatusMap.GetStatusString(
                //    raidInfo.Temperature.Status, raidInfo.Temperature));
                SetToolTip(thermometerPath, RaidStatusMap.GetStatusString(
                    temperature.Status, temperature));
                SetToolTip(levelPath, RaidStatusMap.GetStatusString(
                    temperature.Status, temperature));

                if (temperature.Status == Status.ok)
                {
                    curTempLabel.Foreground = defaultBrush;
                }
                else
                {
                    curTempLabel.Foreground = alertBrush;
                }

                // Calculate new Y value for thermometer mercury
                float tempRange = temperature.MaxExpectedCelcius - temperature.MinExpectedCelcius;
                float tempFractional = temperature.TempCelcius / tempRange;
                float newTemperatureY = 50f - (25f * tempFractional);

                // Calculate the thermometer mercury color
                levelPath.Fill = new SolidColorBrush(Colors.DarkGray);
                if (temperature.TempCelcius <= temperature.MinExpectedCelcius)
                {
                    levelPath.Fill = new SolidColorBrush(Colors.Blue);
                }
                else if (temperature.TempCelcius >= temperature.MaxExpectedCelcius)
                {
                    levelPath.Fill = new SolidColorBrush(Colors.Red);
                }

                // Clip temperature bounds to avoid drawing errors
                if (newTemperatureY < 25f)
                {
                    newTemperatureY = 25f;
                }
                else if (newTemperatureY > 50f)
                {
                    newTemperatureY = 50f;
                }

                // Draw new mercury level
                PathGeometry geometry = levelPath.Data as PathGeometry;
                if (geometry != null)
                {
                    if (Math.Abs(newTemperatureY - oldTemperatureY) >= float.Epsilon)
                    {
                        foreach (PathFigure figure in geometry.Figures)
                        {
                            for (int i = 0; i < 2; i++)
                            {
                                LineSegment lineSegment = figure.Segments[i] as LineSegment;
                                if (lineSegment != null)
                                {
                                    lineSegment.Point = new Point(lineSegment.Point.X, newTemperatureY);
                                }
                            }
                        }
                        oldTemperatureY = newTemperatureY;
                    }
                }
            }
            else
            {
                // No temperature Sensor; so no value to display
                SetToolTip(curTempLabel, StringResources.String_Unknown_Temperature);
                SetToolTip(thermometerPath, StringResources.String_Unknown_Temperature);
                SetToolTip(levelPath, StringResources.String_Unknown_Temperature);
            }
        }

        /// <summary>
        /// Update disk statuses
        /// </summary>
        /// <param name="label"></param>
        /// <param name="disk"></param>
        private static void SetDiskStatus(Label label, RaidDisk disk) {
            string diskInfoString;
            string diskState = "";
            string diskMakeModel = "";
            
            if (disk.DiskState != null) {
                if (disk.DiskState.CompareTo("Sleeping") == 0) {
                    diskInfoString = StringResources.String_sleep;
                    diskState = StringResources.String_sleeping;
                } else {
                    diskInfoString = disk.TempCelcius + StringResources.String_Celsius;
                    diskState = disk.DiskState;
                }
                SetLedStatus(label, disk.Status);
                label.Content = diskInfoString;

                if (disk.Status == Status.ok) {
                    // breakdown the Disk Description to allow reformatting in tooltip
                    // test code for DiskType details
                    //string mytext = "ST3320620AS 298 GB";
                    //string mytext = "SAMSUNG HD103SJ 931 GB";

                    // count number of spaces in string; split string into this number of parts at those spaces

                    //int count = mytext.Split(' ').Length - 1;
                    //string[] splitDiskType = mytext.Split(new char[] { ' ' }, count);

                    int count = disk.DiskType.Split(' ').Length - 1;
                    string[] splitDiskType = disk.DiskType.Split(new char[] { ' ' }, count);
                    if (splitDiskType.Length > 0)
                    {
                        diskMakeModel = splitDiskType[0];
                        for (int i = 1; i < splitDiskType.Length; i++)
                            {
                                diskMakeModel += "\n" + splitDiskType[i];
                            }
                    }
                    if (diskState != "") 
                    {
                        SetToolTip(label, StringResources.String_Channel + disk.Index + ": " + "\n" + diskState + "\n" + diskMakeModel);
                    } else {
                        SetToolTip(label, StringResources.String_Channel + disk.Index + ": " + "\n" + diskMakeModel);
                    }

                } else {
                    SetToolTip(label, RaidStatusMap.GetStatusString(disk.Status, disk));
                }
            }
        }

        /// <summary>
        /// Sets the label color based on the status
        /// </summary>
        /// <param name="label"></param>
        /// <param name="message"></param>
        /// <param name="status"></param>
        private static void SetLabelWithStatus(Label label, string message, Status status) {
            label.Content = message;
            // Alert status
            if (status == Status.ok || status == Status.nas_connection_lost) {
                label.Foreground = defaultBrush;
            } else {
                label.Foreground = alertBrush;
            }
        }

        /// <summary>
        /// Set the tooltip of a control
        /// </summary>
        /// <param name="element"></param>
        /// <param name="text"></param>
        private static void SetToolTip(FrameworkElement element, string text) {
            if (!String.IsNullOrEmpty(text)) {
                ToolTip toolTip = new ToolTip();
                toolTip.FontSize = 10;
                toolTip.ClipToBounds = false;
                toolTip.Content = text;
                element.ToolTip = toolTip;
            } else {
                element.ToolTip = null;
            }
        }

        /// <summary>
        /// Set the LED staus color
        /// </summary>
        /// <param name="led"></param>
        /// <param name="status"></param>
        private static void SetLedStatus(Label led, Status status) {
            if (status == Status.ok) {
                led.Background = new SolidColorBrush(Colors.Lime);
                led.BorderBrush = new SolidColorBrush(Colors.LimeGreen);
            } else if (status == Status.warn || status == Status.resync) {
                led.Background = new SolidColorBrush(Colors.Orange);
                led.BorderBrush = new SolidColorBrush(Colors.DarkOrange);
            } else if (status == Status.nas_connection_lost) {
                led.Background = new SolidColorBrush(Colors.DarkGreen);
                led.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0, 0x4B, 0));
            } else {
                led.Background = new SolidColorBrush(Colors.Red);
                led.BorderBrush = new SolidColorBrush(Colors.DarkRed);
            }
            SetToolTip(led, status.ToString());
        }

        private void Grid_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            // TODO: If somebody can get the code below working true interaction between
            // this gadget and the sidebar host, which is basically internet explorer
            // with some modified security settings, is possible. This opens a world of
            // possibilities like a fly-out for settings or a sizeable GUI...

            //http://blogs.msdn.com/b/llobo/archive/2009/11/03/new-wpf-features-script-interop-in-xbap.aspx

            //// Retrieve the script object. The XBAP must be hosted in a HTML iframe or
            //// the HostScript object will be null.
            //dynamic script = BrowserInteropHelper.HostScript;

            //if (script != null)
            //{
            //    Log("Script isn't null !");
            //    // Call a script function.
            //    // For this to work the function 'func' should be added in javascript to main.html
            //    script.func("Hello host!");
            //}
            //CircularLogBuffer.DumpLog();
        }

        /// <summary>
        /// Helper function to easily add log messages
        /// </summary>
        /// <param name="logEntry"></param>
        private void Log(string logEntry) {
            CircularLogBuffer.Add(logEntry);
        }

        /// <summary>
        /// Called when the gadget is closed. Stop all timers, threads and cleanup.
        /// </summary>
        private void Dispatcher_ShutdownStarted(object sender, EventArgs e) {
            ExitPolling();
        }
    }
}
