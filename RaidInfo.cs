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
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using RaidarGadget.Properties;

namespace RaidarGadget {

    // Type declarations

    /// <summary>
    /// The status of several components of the device
    /// </summary>
    public enum Status {
        /// <summary>
        /// 'short': "Normal"
        /// 'criticality': 'none'
        /// 'desc': "Normal operating mode"
        /// </summary>
        ok,
        /// <summary>
        /// 
        /// </summary>
        not_ok,
        /// <summary>
        /// 
        /// </summary>
        unknown,
        /// <summary>
        /// 'short': "Awaiting resync"
        /// 'criticality': 'temporary'
        /// 'desc': "Waiting to resync to the RAID volume"
        /// </summary>
        resync,
        /// <summary>
        /// 'short': "Warning"
        /// 'criticality': 'vulnerable'
        /// 'desc': "Device is in a state where it needs attention"
        /// </summary>
        warn,
        /// <summary>
        /// 'short': "Life support mode"
        /// 'criticality': 'critical'
        /// 'desc': "Multiple disk failures detected"
        /// </summary>
        life_support,
        /// <summary>
        /// 'short': "Awaiting recovery"
        /// 'criticality': 'critical'
        /// 'desc': "Disc awaiting recovery"
        /// </summary>
        awaiting_recovery,
        /// <summary>
        /// 'short': "Inactive spare"
        /// 'criticality': 'none'
        /// 'desc': "Disk is a spare disk on standby"
        /// </summary>
        spare_inactive,
        /// <summary>
        /// 'short': "Not Present"
        /// 'criticality': 'none'
        /// 'desc': "No device attached"
        /// </summary>
        not_present,
        /// <summary>
        /// 'short': "Dead"
        /// 'criticality': 'fatal'
        /// 'desc': "Device has failed"
        /// </summary>
        fail,
        /// <summary>
        /// 'short': "Dead"
        /// 'criticality': 'fatal'
        /// 'desc': "Device has failed"
        /// </summary>
        dead,
        /// <summary>
        /// 'short': "NasConnectionLost"
        /// 'criticality': 'none'
        /// 'desc': "Network connection with the ReadyNAS is lost"
        /// </summary>
        nas_connection_lost
    }

    /// <summary>
    /// The criticality of a components status of the device
    /// </summary>
    public enum Criticality {
        temporary,
        vulnerable,
        critical,
        fatal,
        none
    }

    /// <summary>
    /// Maps all the possible device statuses to their criticality, their
    /// description and their (localized) resource string. This class will
    /// be used to update the UI with correct status message when an error
    /// occurs.  
    /// </summary>
    public static class RaidStatusMap {

        private static Dictionary<Status, string> descriptions = new Dictionary<Status, string>();

        private static Dictionary<Status, string> criticalities = new Dictionary<Status, string>();

        static RaidStatusMap() {
            descriptions.Add(Status.ok, Resources.Status_ok);
            criticalities.Add(Status.ok, "");

            descriptions.Add(Status.warn, Resources.Status_warn);
            criticalities.Add(Status.warn, Resources.Criticality_temporary);

            descriptions.Add(Status.resync, Resources.Status_resync);
            criticalities.Add(Status.resync, Resources.Criticality_temporary);

            descriptions.Add(Status.life_support, Resources.Status_life_support);
            criticalities.Add(Status.life_support, Resources.Criticality_critical);

            descriptions.Add(Status.awaiting_recovery, Resources.Status_awaiting_recovery);
            criticalities.Add(Status.awaiting_recovery, Resources.Criticality_critical);

            descriptions.Add(Status.spare_inactive, Resources.Status_spare_inactive);
            criticalities.Add(Status.spare_inactive, "");

            descriptions.Add(Status.not_present, Resources.Status_not_present);
            criticalities.Add(Status.not_present, "");

            descriptions.Add(Status.fail, Resources.Status_fail);
            criticalities.Add(Status.fail, Resources.Criticality_fatal);

            descriptions.Add(Status.dead, Resources.Status_dead);
            criticalities.Add(Status.dead, Resources.Criticality_fatal);
        }

        /// <summary>
        /// Retrieves the corresponding status string for a RaidInfo object
        /// </summary>
        /// <param name="status"></param>
        /// <param name="statusObject"></param>
        /// <returns></returns>
        public static string GetStatusString(Status status, object statusObject) {
            string statusString = Resources.Status_ok;

            string tempString;
            if (statusObject is RaidDisk) {
                if (descriptions.TryGetValue(status, out tempString)) {
                    statusString = tempString;
                    if (criticalities.TryGetValue(status, out tempString)) {
                        statusString += tempString;
                    }
                } else {
                    statusString = Resources.Status_disk_not_ok;
                }
            } else if (statusObject is RaidVolume) {
                if (descriptions.TryGetValue(status, out tempString)) {
                    statusString = tempString;
                    if (criticalities.TryGetValue(status, out tempString)) {
                        statusString += tempString;
                    }
                } else {
                    statusString = Resources.Status_volume_not_ok;
                }
            } else if (statusObject is RaidTemperature) {
                if (status != Status.ok) {
                    statusString = Resources.String_Temperature_sensor + "\n" +
                        Resources.Status_temp_not_ok;
                } else {
                    statusString = Resources.String_Temperature_sensor + "\n" + statusString;
                }
            } else if (statusObject is RaidUPS) {
                if (status != Status.ok) {
                    statusString = Resources.Status_ups_not_ok;
                }
            } else if (statusObject is RaidFan) {
                if (status != Status.ok) {
                    statusString = Resources.String_Fan + "\n" + Resources.Status_fan_not_ok;
                } else {
                    statusString = Resources.String_Fan + "\n" + statusString;
                }
            } else {
                if (status != Status.ok) {
                    statusString = Resources.Status_default_not_ok;
                }
            }

            return statusString;
        }

    }

    /// <summary>
    /// Parses raw raidar SNMP packet data and converts it to a RaidInfo
    /// data oject hierarchy.
    /// </summary>
    [ComVisible(false)]
    public class RaidInfo {

        // Constants
        private const int HeaderByteCount = 28;

        // Create the Regex as static to speed up run time parsing, compiling
        // the Regex each time it is needed is a costly operation!
        private static Regex propertySplitRegEx = new Regex(@"\n", RegexOptions.Compiled);
        private static Regex propertyRegEx = new Regex(@"^(([a-z]+)+!!([0-9])!!)", RegexOptions.Compiled);
        private static Regex modelRegEx = new Regex(@"mode=[a-z]+::descr=([^:]+).*", RegexOptions.Compiled);
        private static Regex firmwareNameRegEx = new Regex(@"^([a-zA-Z]+).*", RegexOptions.Compiled);
        private static Regex firmwareVersionRegEx = new Regex(@".*version=([^,]+).*", RegexOptions.Compiled);

        private string rawPacketData;
        private string versionUnparsed;
        private string htmlID;

        private string mac;
        private string name;
        private string ip;
        private RaidTemperature temp;
        private RaidFan fan;
        private RaidUPS ups;
        private List<RaidVolume> volumes = new List<RaidVolume>();
        private List<RaidDisk> disks = new List<RaidDisk>();
        private string model;
        private string software;
        private string version;
        //private int upTime;
        private string bootFlag;

        /// <summary>
        /// Gets the device name
        /// </summary>
        public string Name {
            get {
                return name;
            }
        }

        /// <summary>
        /// Gets the device IP address
        /// </summary>
        public string IpAdress {
            get {
                return ip;
            }
        }

        /// <summary>
        /// Gets the device MAC address
        /// </summary>
        public string MacAdress {
            get {
                return mac;
            }
        }

        /// <summary>
        /// Gets the device <see cref="RaidTemperature"/> structure.
        /// </summary>
        public RaidTemperature Temperature {
            get {
                return temp;
            }
        }

        /// <summary>
        /// Gets the device <see cref="RaidFan"/> structure.
        /// </summary>
        public RaidFan Fan {
            get {
                return fan;
            }
        }

        /// <summary>
        /// Gets the device <see cref="RaidUPS"/> structure.
        /// </summary>
        public RaidUPS Ups {
            get {
                return ups;
            }
        }

        /// <summary>
        /// Gets the list of <see cref="RaidVolume"/> of the device.
        /// </summary>
        public List<RaidVolume> Volumes {
            get {
                return volumes;
            }
        }

        /// <summary>
        /// Gets the list of <see cref="RaidDisk"/> of the device.
        /// </summary>
        public List<RaidDisk> Disks {
            get {
                return disks;
            }
        }

        /// <summary>
        /// Gets the device model
        /// </summary>
        public string Model {
            get {
                return model;
            }
        }

        /// <summary>
        /// Gets the device software name
        /// </summary>
        public string SoftwareName {
            get {
                return software;
            }
        }

        /// <summary>
        /// Gets the device 
        /// </summary>
        public string SoftwareVersion {
            get {
                return version;
            }
        }

        public void Parse(string rawData) {
            volumes.Clear();
            disks.Clear();

            // Remove the raw header as it contains no(?) usable info
            rawData = rawData.Remove(0, HeaderByteCount);

            // Raw package to test specific cases
            //rawData = "00:0d:a2:01:09:bd\tNASgul\t192.168.1.5\tmodel!!0!!mode=pro::descr=ReadyNAS NV::arch=nsp\n" +
            //    "fan!!0!!status=ok::descr=2352RPM\n" +
            //    "temp!!0!!status=ok::descr=34.0C\n" +
            //    "disk!!1!!status=ok::descr=Channel 1: ST3320620AS 298 GB\n" +
            //    "disk!!2!!status=ok::descr=Channel 2: ST3320620AS 298 GB\n" +
            //    "disk!!3!!status=ok::descr=Channel 3: ST3320620AS 298 GB\n" +
            //    "disk!!4!!status=ok::descr=Channel 4: ST3320620AS 298 GB\n" +
            //    "\tFS_CHECK\n" +
            //    "\t66\t1\t1\n";

            // Test: Szerda
            //rawData = "e0:91:f5:f9:9c:da\tXYZ-NAS\t192.168.0.1\tfan!!1!!status=ok::descr=1630RPM\n" +
            //    "ups!!1!!status=not_present::descr=\n" +
            //    "volume!!1!!status=ok::descr= C: RAID Level X, ; 140 GB (15%)  921 GB\n"+
            //    "disk!!1!!status=ok::descr= 1: SAMSUNG HD103SJ 931 GB, 39C/102F\n" +
            //    "disk!!2!!status=ok::descr= 2: SAMSUNG HD103SJ 931 GB, 38C/100F;31 ATA Errors\n" +
            //    "model!!0!!mode=home::descr=ReadyNAS Duo::arch=nsp\n" +
            //    "\tRAIDiator!!version=4.1.8,time=1314924646\n" +
            //    "\t66\n";

            // Split the raw data
            string[] outerArr = rawData.Split('\t');
            if (outerArr.Length < 5) {
                throw new InvalidOperationException("Invalid data format");
            }

            // Split the raw properties
            string[] propertyArray = propertySplitRegEx.Split(outerArr[3]);
            if (propertyArray.Length < 1) {
                throw new InvalidOperationException("Invalid data in property lists");
            }

            rawPacketData = rawData;

            mac = outerArr[0];
            name = outerArr[1];
            ip = outerArr[2];
            htmlID = "id-"+mac;

            foreach (string propertyString in propertyArray) {
                int id;
                string type = string.Empty;
                string property = string.Empty;
                id = 0;
                string[] splitStrings = propertyRegEx.Split(propertyString);
                if (splitStrings.Length >= 5) {
                    type = splitStrings[2];
                    id = Int32.Parse(splitStrings[3]);
                    property = splitStrings[4];

                    switch (type) {
                        case "temp": {
                                temp = new RaidTemperature(property);
                                break;
                            }
                        case "fan": {
                                fan = new RaidFan(property);
                                break;
                            }
                        case "ups": {
                                ups = new RaidUPS(property);
                                break;
                            }
                        case "volume": {
                                RaidVolume volume = new RaidVolume(property, id);
                                volumes.Add(volume);
                                break;
                            }
                        case "disk": {
                                RaidDisk disk = new RaidDisk(property, id);
                                disks.Add(disk);
                                break;
                            }
                        case "model": {
                                Match match = modelRegEx.Match(property);

                                model = "Unknown ReadyNAS model";
                                if (match.Groups.Count >= 2) {
                                    model = match.Groups[1].Value;
                                }
                                break;
                            }
                        default: {
                                // just ignore
                                break;
                            }
                    }
                }
            }

            versionUnparsed = outerArr[4];

            // Get firmware software name:
            Match patternMatch = firmwareNameRegEx.Match(versionUnparsed);
            if (patternMatch.Groups.Count >= 2) {
                software = patternMatch.Groups[1].Value;
            } else {
                software = "Unknown NAS firmware";
            }

            // Get firmware software version:
            patternMatch = firmwareVersionRegEx.Match(versionUnparsed);
            if (patternMatch.Groups.Count >= 2) {
                version = patternMatch.Groups[1].Value;
            } else {
                version = "Unknown version";
            }

            // TODO: Get device time???
            //r = new Regex(@".*time=([0-9]+).*", RegexOptions.Compiled);
            //patternMatch = r.Match(versionUnparsed);
            //if (patternMatch.Groups.Count >= 2)
            //{
            //    upTime = Int32.Parse(patternMatch.Groups[1].Value);
            //    TimeSpan upTimeSpan = new TimeSpan(upTime);
            //}
            //else
            //{
            //    upTime = -1;
            //}

            this.bootFlag = outerArr[5];
        }

    }

    /// <summary>
    /// Contains the temperatures of the device.
    /// </summary>
    public class RaidTemperature {
        private static Regex tempRegEx = new Regex(@"^status=([a-z_]+)::descr=([0-9\.]+)C/([0-9\.]+)F::expected=([0-9]+)-([0-9]+)C/([0-9]+)-([0-9]+)F", RegexOptions.Compiled);

        private Status status = Status.unknown;
        private float tempCelcius;
        private float tempFahrenheit;
        private int minExpectedCelcius;
        private int maxExpectedCelcius;
        private int minExpectedFahrenheit;
        private int maxExpectedFahrenheit;

        /// <summary>
        /// Gets the temperature status
        /// </summary>
        public Status Status {
            get {
                return status;
            }
        }

        /// <summary>
        /// Gets the temperature in Celcius
        /// </summary>
        public float TempCelcius {
            get {
                return tempCelcius;
            }
        }

        /// <summary>
        /// Gets the temperature in Fahrenheit
        /// </summary>
        public float TempFahrenheit {
            get {
                return tempFahrenheit;
            }
        }

        /// <summary>
        /// Gets the minimum expected temperature in Celcius
        /// </summary>
        public int MinExpectedCelcius {
            get {
                return minExpectedCelcius;
            }
        }

        /// <summary>
        /// Gets the maximum expected temperature in Celcius
        /// </summary>
        public int MaxExpectedCelcius {
            get {
                return maxExpectedCelcius;
            }
        }

        /// <summary>
        /// Gets the minimum expected temperature in Fahrenheit
        /// </summary>
        public int MinExpectedFahrenheit {
            get {
                return minExpectedFahrenheit;
            }
        }

        /// <summary>
        /// Gets the maximum expected temperature in Fahrenheit
        /// </summary>
        public int MaxExpectedFahrenheit {
            get {
                return maxExpectedFahrenheit;
            }
        }

        /// <summary>
        /// Construct a <see cref="RaidTemperature"/> from a unparsed
        /// temperature string.
        /// </summary>
        /// <param name="unparsedTemperature"></param>
        public RaidTemperature(string unparsedTemperature) {
            Match match = tempRegEx.Match(unparsedTemperature);

            if (match.Groups.Count == 8) {
                string statusString = match.Groups[1].Value;

                if (!Status.TryParse(statusString, out status)) {
                    status = Status.unknown;
                }

                // Parse with a '.' for decimal point by using a US culture object
                CultureInfo usCulture = new CultureInfo("en-US");
                tempCelcius = float.Parse(match.Groups[2].Value,
                    NumberStyles.Float, usCulture);
                tempFahrenheit = float.Parse(match.Groups[3].Value,
                    NumberStyles.Float, usCulture);
                minExpectedCelcius = Int32.Parse(match.Groups[4].Value);
                maxExpectedCelcius = Int32.Parse(match.Groups[5].Value);
                minExpectedFahrenheit = Int32.Parse(match.Groups[6].Value);
                maxExpectedFahrenheit = Int32.Parse(match.Groups[7].Value);
            }
        }
    }

    /// <summary>
    /// Contains the fan information of the device.
    /// </summary>
    public class RaidFan {
        private static Regex fanRegEx = new Regex(@"^status=([a-z_]+)::descr=([0-9]+)RPM", RegexOptions.Compiled);

        private Status status = Status.unknown;
        private string fanSpeed;

        /// <summary>
        /// Gets the fan status
        /// </summary>
        public Status Status {
            get {
                return status;
            }
        }

        /// <summary>
        /// Gets the fan speed
        /// </summary>
        public string FanSpeed {
            get {
                return fanSpeed;
            }
        }

        /// <summary>
        /// Construct a <see cref="RaidFan"/> from unparsed fan data.
        /// </summary>
        /// <param name="unparsedTemperature"></param>
        public RaidFan(string unparsedFan) {
            Match match = fanRegEx.Match(unparsedFan);

            if (match.Groups.Count == 3) {
                string statusString = match.Groups[1].Value;
                if (!Status.TryParse(statusString, out status)) {
                    status = Status.unknown;
                }
                fanSpeed = match.Groups[2].Value;
            }
        }
    }

    /// <summary>
    /// The UPS information of the device.
    /// </summary>
    public class RaidUPS {
        private static Regex upsRegEx = new Regex(@"^status=([a-z_]+)::descr=([.]+)*", RegexOptions.Compiled);
        private static Regex upsDescrRegEx = new Regex(@"([.]+)\nBattery\scharge:\s*([0-9]+)%,\s*([.]+)*", RegexOptions.Compiled);

        private Status status = Status.unknown;
        private string description = "";
        private string charge = "";
        private string timeLeft = "";

        /// <summary>
        /// The UPS status.
        /// </summary>
        public Status Status {
            get {
                return status;
            }
        }

        /// <summary>
        /// The UPS description
        /// </summary>
        public string Description {
            get {
                return description;
            }
        }

        /// <summary>
        /// The UPS battery charge
        /// </summary>
        public string Charge {
            get {
                return charge;
            }
        }

        /// <summary>
        /// The UPS time left on the current battery charge
        /// </summary>
        public string TimeLeft {
            get {
                return timeLeft;
            }
        }

        /// <summary>
        /// Constructs a <see cref="RaidUPS"/> from a unparsed UPS string.
        /// </summary>
        /// <param name="unparsedTemperature"></param>
        public RaidUPS(string unparsedUps) {
            Match match = upsRegEx.Match(unparsedUps);

            if (match.Groups.Count >= 3) {
                string statusString = match.Groups[1].Value;
                if (!Status.TryParse(statusString, out status)) {
                    status = Status.unknown;
                }
                if (status != RaidarGadget.Status.not_present) {
                    Match descrMatch = upsDescrRegEx.Match(match.Groups[2].Value);
                    if (match.Groups.Count >= 4) {
                        description = descrMatch.Groups[1].Value;
                        charge = descrMatch.Groups[2].Value;
                        timeLeft = descrMatch.Groups[3].Value;
                    }
                }
            }
        }
    }

    /// <summary>
    /// The status and information of a volume on the device.
    /// </summary>
    public class RaidVolume {
        private static Regex volumeRegEx = new Regex(@"^status=([a-z_]+)::descr=([\w\W]+):\s*([\w\W]+),\s*([\w\W]+);\s*([0-9]+)[^0-9]+([0-9]+)[^0-9]+([0-9]+).*", RegexOptions.Compiled);

        private Status status = Status.unknown;
        private string name;
        private int index;
        private string raidLevel;
        private int gbUsed;
        private int gbTotal;
        private string raidStatus;

        /// <summary>
        /// Gets the status of the volume
        /// </summary>
        public Status Status {
            get {
                return status;
            }
        }

        /// <summary>
        /// Gets the name of the volume
        /// </summary>
        public string Name {
            get {
                return name;
            }
        }

        /// <summary>
        /// Gets the index of the volume
        /// </summary>
        public int Index {
            get {
                return index;
            }
        }

        /// <summary>
        /// Gets the RAID level of the volume
        /// </summary>
        public string RaidLevel {
            get {
                return raidLevel;
            }
        }

        /// <summary>
        /// The used space of the volume
        /// </summary>
        public int GbUsed {
            get {
                return gbUsed;
            }
        }

        /// <summary>
        /// The size of the volume
        /// </summary>
        public int GbTotal {
            get {
                return gbTotal;
            }
        }

        /// <summary>
        /// The RAID status string of the volume
        /// </summary>
        public string RaidStatus {
            get {
                return raidStatus;
            }
        }

        /// <summary>
        /// Construct a <see cref="RaidUPS"/> from a unparsed UPS string.
        /// </summary>
        /// <param name="unparsedTemperature"></param>
        public RaidVolume(string unparsedVolume, int id) {
            index = id;
            Match match = volumeRegEx.Match(unparsedVolume);

            if (match.Groups.Count >= 7) {
                string statusString = match.Groups[1].Value;
                if (!Status.TryParse(statusString, out status)) {
                    status = Status.unknown;
                }
                name = match.Groups[2].Value;
                raidLevel = match.Groups[3].Value;
                raidStatus = match.Groups[4].Value;
                gbUsed = Int32.Parse(match.Groups[5].Value);
                gbTotal = Int32.Parse(match.Groups[7].Value);
            }
        }
    }

    /// <summary>
    /// The status and information of a disk on the device.
    /// </summary>
    public class RaidDisk {
        private static Regex diskRegEx = new Regex(@"^status=([a-z_]+)::descr=([^:]+):[ ]*([^,]+),[ ]*([0-9]+)[^0-9]+([0-9]+).\[*([^\]]+)*.*", RegexOptions.Compiled);

        private Status status = Status.unknown;
        private int index;
        private string diskChannel;
        private string diskType;
        private string diskState;
        private int tempCelcius;
        private int tempFahrenheit;

        /// <summary>
        /// Gets the status of the disk
        /// </summary>
        public Status Status {
            get {
                return status;
            }
        }

        /// <summary>
        /// Gets the index of the disk
        /// </summary>
        public int Index {
            get {
                return index;
            }
        }

        /// <summary>
        /// Gets the channel number that the disk is connected to
        /// </summary>
        public string DiskChannel {
            get {
                return diskChannel;
            }
        }

        /// <summary>
        /// Gets the disk drive's model name
        /// </summary>
        public string DiskType {
            get {
                return diskType;
            }
        }

        /// <summary>
        /// Gets the disk size
        /// </summary>
        public string DiskSize {
            get {
                return diskType;
            }
        }

        /// <summary>
        /// Gets the disk temperature in Celcius
        /// </summary>
        public int TempCelcius {
            get {
                return tempCelcius;
            }
        }

        /// <summary>
        /// Gets the disk temperature in Fahrenheit
        /// </summary>
        public int TempFahrenheit {
            get {
                return tempFahrenheit;
            }
        }

        /// <summary>
        /// Gets the current state of the disk
        /// Possible states:
        ///  0: Online
        ///  1: Offline
        /// </summary>
        public string DiskState {
            get {
                return diskState;
            }
        }

        /// <summary>
        /// Construct a <see cref="RaidDisk"/> from a unparsed disk string.
        /// </summary>
        /// <param name="unparsedTemperature"></param>
        public RaidDisk(string unparsedDisk, int id) {
            index = id;
            Match match = diskRegEx.Match(unparsedDisk);

            if (match.Groups.Count >= 6) {
                string statusString = match.Groups[1].Value;
                if (!Status.TryParse(statusString, out status)) {
                    status = Status.unknown;
                }
                diskChannel = match.Groups[2].Value;
                diskType = match.Groups[3].Value;
                tempCelcius = Int32.Parse(match.Groups[4].Value);
                tempFahrenheit = Int32.Parse(match.Groups[5].Value);
                if (match.Groups.Count >= 7) {
                    diskState = match.Groups[6].Value;
                } else {
                    diskState = "Active";
                }
            }
        }
    }

}
