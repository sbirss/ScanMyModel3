//#define disablebluetooth
//#define VERBOSE
#define USEDCB

using DBCLib;
using Android.OS;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using Android.Content;
using Android.App;
using Android.Content.Res;
using System.Text;

namespace TeslaSCAN {

  [Serializable]
  public class Parser {

    CustomAdapter adapter;
    public ConcurrentDictionary<string, ListElement> items;
    SortedList<int, Packet> packets;
    public List<List<ListElement>> ignoreList;
    long time; // if I was faster I'd use 'short time'.... :)
    int numUpdates;
    int numCells;
    public char[] tagFilter;
    public bool fastLogEnabled;
    private StreamWriter fastLogStream;
    private List<Value> fastLogItems;
    char separator = ',';
    Stopwatch logTimer;
    private MainActivity mainActivity;
        System.IO.Stream inputStream;


        public const double miles_to_km = 1.609344;
    public const double kw_to_hp = 1.34102209;
    public const double nm_to_ftlb = 0.737562149;
    double nominalFullPackEnergy;
        int UNIX_TIME;
    double amp;
    double volt;
    double power;
    double mechPower;
    double fMechPower;
    double speed;
    double drivePowerMax;
    double torque;
    double chargeTotal;
    double dischargeTotal;
    double odometer;
    double tripDistance;
    double charge;
    double discharge;

    private double frTorque;
    private double dcChargeTotal;
    private double acChargeTotal;
    private double regenTotal;
    private double energy;
    private double regen;
    private double acCharge;
    private double dcCharge;
    private double nominalRemaining;
    private double buffer;
    private double soc;
    private double fl;
    private double fr;
    private double rl;
    private double rr;
    private int frpm;
    private int rrpm;
    private bool feet;
    private bool seat;
    private bool win;
    private long resetGaugeTime;
    private int dcIn;
    private double dcOut;
    private double fDissipation;
    private double combinedMechPower;
    private double rDissipation;
    private double rInput;
    private double fInput;
    private double hvacPower;
    private bool dissipationUpdated;
    private long dissipationTimeStamp;

        static UInt64 ByteSwap64(UInt64 n)
        {
            UInt64 n_swapped = 0;
            for (int byte_index = 7; byte_index >= 0; --byte_index)
            {
                n_swapped <<= 8;
                n_swapped |= n % (1 << 8);
                n >>= 8;
            }
            return n_swapped;
        }

        static double ExtractSignalFromBytes(byte[] bytes, DBCLib.Message.Signal signal)
        {
            UInt64 signalMask = 0;
            for (int bit_index = (int)(signal.StartBit + signal.BitSize - 1); bit_index >= 0; --bit_index)
            {
                signalMask <<= 1;
                if (bit_index >= signal.StartBit)
                {
                    signalMask |= 1;
                }
            }

            UInt64 signalValueRaw = 0;
            for (int byte_index = bytes.Length - 1; byte_index >= 0; --byte_index)
            {
                signalValueRaw <<= 8;
                signalValueRaw += bytes[byte_index];
            }

            signalValueRaw &= signalMask;

            if (signal.ByteOrder == DBCLib.Message.Signal.ByteOrderEnum.BigEndian)
            {
                signalMask = ByteSwap64(signalMask);
                signalValueRaw = ByteSwap64(signalValueRaw);
            }

            while ((signalMask & 0x1) == 0)
            {
                signalValueRaw >>= 1;
                signalMask >>= 1;
            }

            double signalValue = signalValueRaw;

            if (signal.ValueType == DBCLib.Message.Signal.ValueTypeEnum.Signed)
            {
                UInt64 signalMaskHighBit = (signalMask + 1) >> 1;
                if ((signalValueRaw & signalMaskHighBit) != 0)
                {
                    signalValue = -(Int64)((signalValueRaw ^ signalMask) + 1);
                }
            }

            signalValue *= signal.ScaleFactor;
            signalValue += signal.Offset;

            return signalValue;
        }


        public Parser(MainActivity mainActivity, CustomAdapter adapter) {
      this.adapter = adapter;
      this.mainActivity = mainActivity;
      items = new ConcurrentDictionary<string, ListElement>();
      packets = new SortedList<int, Packet>();
      time = SystemClock.ElapsedRealtime() + 1000;

      /* tags:
          p: performance
          t: trip
          b: battery
          c: temperature
          f: front drive unit
          s: startup (app will wait until these packets are found before starting 'normal' mode)
          i: imperial
          m: metric
          i: ignore (in trip tabs, with slow/ELM adapters)
          z: BMS
          x: Cells
          e: efficiency
      */

      Packet p;


#if USEDCB

            //String dbcPath = "Model3CAN.dbc";
            //string path = Directory.GetCurrentDirectory();
            //var pathFile = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);
            ////IEnumerable<string> files2 = Directory.EnumerateFiles((String)pathFile, "*.dbc");
            //dbcPath = (string)pathFile + "/" + dbcPath;
            //inputStream = mainActivity.Assets.Open("CAN_BUS_LOG_Feb_10.txt");

            //LINES 185 to 239 are taken from CANBUS-Analyzer https://github.com/amund7/CANBUS-Analyzer
            //if (dbcPath != null)
            if (true)
            {
                Reader reader = new DBCLib.Reader();

                reader.AllowErrors = true;

                //List<object> entries = reader.Read(dbcPath);
                //AssetManager assets = this.Assets;
                List<KeyValuePair<uint, string>> errors = null;
                List< KeyValuePair<uint, string> > warnings = null;
                using (StreamReader streamReader = new StreamReader(mainActivity.Assets.Open("Model3CAN.dbc"), Encoding.Default, false))
                {
                    List<object> entries = reader.Read(streamReader, "Model3CAN.dbc", errors, warnings);

                    //List<object> entries = reader.Read(mainActivity.Assets.Open("Model3CAN.dbc"));

                    foreach (object entry in entries)
                    {
                        if (entry is DBCLib.Message)
                        {
                            DBCLib.Message message = (DBCLib.Message)entry;

                            packets.Add((int)message.Id, p = new Packet((int)message.Id, this));
                            foreach (DBCLib.Message.Signal signal in message.Signals)
                            {
                                var valueLookup = (DBCLib.Value)
                                  entries.Where(x => x is DBCLib.Value && ((DBCLib.Value)x).ContextSignalName == signal.Name).FirstOrDefault();
                                p.AddValue(
                                  signal.Name,//.Replace("_", " "),
                                  signal.Unit,
                                  signal.Name,
                                  (bytes) =>
                                  {
                                      double result;
                                      if (signal.StartBit + signal.BitSize > bytes.Length * 8) // check data length
                                      return 0;
                                      if (signal.Multiplexer) // if this is our multiplex / page selector
                                      return
                                        p.currentMultiplexer = // store it
                                          ExtractSignalFromBytes(bytes, signal); // and return it
                                  else if (signal.MultiplexerIdentifier != null)
                                      { // else if this is a sub-item
                                      if (signal.MultiplexerIdentifier == p.currentMultiplexer) // check if we're on the same page
                                          result = ExtractSignalFromBytes(bytes, signal); // then return it
                                      else return 0;
                                      }
                                      else result = ExtractSignalFromBytes(bytes, signal);
                                      if (valueLookup != null)
                                      {
                                          string s =
                                    valueLookup.Mapping.Where(x => x.Key == result).FirstOrDefault().Value; //TryGetValue((long)result, out s);
                                      if (s != null)
                                              return 0;
                                      //return s;
                                  }
                                      return result;
                                  },
                                  null
                                  );
                            }
                        }
                    }
                }
            }




#else

            /*packets.Add(0x256, p = new Packet(0x256, this));
            p.AddValue("Metric", "bool", "s", (bytes) => {
              metric = Convert.ToBoolean(bytes[3] & 0x80);
              if (metric) {
                foreach (var packet in packets)
                  foreach (var v in packet.Value.values)
                    if (v.tag.Contains("i"))
                      packet.Value.values.Remove(v);
              } else {
                foreach (var packet in packets)
                  foreach (var v in packet.Value.values)
                    if (v.tag.Contains("m"))
                      packet.Value.values.Remove(v);
              } 
              return metric ? 1 : 0;
            });*/
            packets.Add(0x528, p = new Packet(0x528, this));
            p.AddValue("UNIX Time", "Sec", "br", (bytes) => (bytes[0] << 8*3) + (bytes[1] << 8*2) + (bytes[2]<<8) +  bytes[3]);

            packets.Add(0x266, p = new Packet(0x266, this));
            p.AddValue("Rr inverter 12V", "V12", "", (bytes) => bytes[0] / 10.0);
            p.AddValue("Rr power", " kW", "e", (bytes) => mechPower =
                ((bytes[2] + ((bytes[3] & 0x7) << 8)) - (512 * (bytes[3] & 0x4))) / 2.0);
            //p.AddValue("Rr mech power HP", "HP", "pf", (bytes) => mechPower * kw_to_hp);
            p.AddValue("Rr dissipation", " kW", "", (bytes) => {
                rDissipation = bytes[1] * 125.0 / 1000.0;
                /*dissipationUpdated = true;
                dissipationTimeStamp = DateTime.Now.Millisecond;*/
                return rDissipation;
            });
            p.AddValue("Rr stator current", "A", "", (bytes) => bytes[4] + ((bytes[5] & 0x7) << 8));
            p.AddValue("Rr regen power max", "KW", "b", (bytes) => (bytes[7] * 4) - 200);
            p.AddValue("Rr drive power max", "KW", "b", (bytes) => drivePowerMax =
                (((bytes[6] & 0x3F) << 5) + ((bytes[5] & 0xF0) >> 3)) + 1);


            packets.Add(0x132, p = new Packet(0x132, this));
            p.AddValue("Battery voltage", " V", "bpr", (bytes) => volt =
                (bytes[0] + (bytes[1] << 8)) / 100.0);
            p.AddValue("Battery current", " A", "b", (bytes) => amp =
                1000 - ((Int16)((((bytes[3]) << 8) + bytes[2]))) / 10.0);
            p.AddValue("Battery power", " kW", "bpe", (bytes) => power = amp * volt / 1000.0);

            packets.Add(0x3B6, p = new Packet(0x3B6, this));
            p.AddValue("Odometer", "Km", "b",
                (bytes) => odometer = (bytes[0] + (bytes[1] << 8) + (bytes[2] << 16) + (bytes[3] << 24)) / 1000.0);

            packets.Add(0x154, p = new Packet(0x154, this));
            p.AddValue("Rr torque measured", "Nm", "p", (bytes) => torque =
               (bytes[5] + ((bytes[6] & 0x1F) << 8) - (512 * (bytes[6] & 0x10))) * 0.25);

            packets.Add(0x108, p = new Packet(0x108, this));
            p.AddValue("Rr motor RPM", "RPM", "",
                (bytes) => rrpm = (bytes[5] + (bytes[6] << 8)) - (512 * (bytes[6] & 0x80)));

            packets.Add(0x376, p = new Packet(0x376, this));
            p.AddValue("Inverter temp 1", " C", "e",
              (bytes) => (bytes[0] - 40));
            p.AddValue("Inverter temp 2", " C", "e",
              (bytes) => (bytes[1] - 40));
            p.AddValue("Inverter temp 3", " C", "e",
              (bytes) => (bytes[2] - 40));
            p.AddValue("Inverter temp 4", " C", "e",
                (bytes) => (bytes[4] - 40));


            packets.Add(0x292, p = new Packet(0x292, this));
      p.AddValue("SOC UI", "%", "br", (bytes) => (bytes[0] + ((bytes[1] & 0x3) << 8)) / 10.0);
      p.AddValue("SOC Min", "%", "br", (bytes) => ((bytes[1] >> 2) + ((bytes[2] & 0xF) << 6)) / 10.0);
      p.AddValue("SOC Max", "%", "br", (bytes) => ((bytes[2] >> 4) + ((bytes[3] & 0x3F) << 4)) / 10.0);
      p.AddValue("SOC Avg", "%", "br", (bytes) => ((bytes[3] >> 6) + ((bytes[4]) << 2)) / 10.0);



            packets.Add(0x252, p = new Packet(0x252, this));
            p.AddValue("Max discharge power", "kW", "b", (bytes) => (bytes[2] + (bytes[3] << 8)) / 100.0);
            p.AddValue("Max regen power", "kW", "b", (bytes) => (bytes[0] + (bytes[1] << 8)) / 100.0);

            packets.Add(0x2A4, p = new Packet(0x2A4, this));
            p.AddValue("7 bit 0", "b", "br",
              (bytes) => (bytes[0] & 0x7F));
            p.AddValue("5 bit 1", "b", "br",
              (bytes) => (bytes[1] & 0xF8) >> 3);
            p.AddValue("5 bit 2", "b", "br",
              (bytes) => ((bytes[1] & 0x7) << 2) + ((bytes[2] & 0xC0) >> 6));
            p.AddValue("7 bit 3", "b", "br",
              (bytes) => (bytes[3] & 0x7F));
            p.AddValue("7 bit 4", "b", "br",
              (bytes) => (bytes[4] & 0xFE) >> 1);

            /*p.AddValue("33A 12 bit 3", "b", "br",
            (bytes) => (bytes[3] + ((bytes[4] & 0x0F) << 8)));
            p.AddValue("33A 12 bit 4", "b", "br",
            (bytes) => (((bytes[4] & 0xF0) >> 4) + ((bytes[5]) << 4)));
            p.AddValue("33A 12 bit 5", "b", "br",
            (bytes) => (bytes[6] + ((bytes[7] & 0x0F) << 8)));*/


            packets.Add(0x352, p = new Packet(0x352, this));
            p.AddValue("Nominal full pack", "kWh", "br", (bytes) => nominalFullPackEnergy = (bytes[0] + ((bytes[1] & 0x03) << 8)) * 0.1);
            p.AddValue("Nominal remaining", "kWh", "br", (bytes) => nominalRemaining = ((bytes[1] >> 2) + ((bytes[2] & 0x0F) * 64)) * 0.1);
            p.AddValue("Expected remaining", "kWh", "r", (bytes) => ((bytes[2] >> 4) + ((bytes[3] & 0x3F) * 16)) * 0.1);
            p.AddValue("Ideal remaining", "kWh", "r", (bytes) => ((bytes[3] >> 6) + ((bytes[4] & 0xFF) * 4)) * 0.1);
            p.AddValue("To charge complete", "kWh", "", (bytes) => (bytes[5] + ((bytes[6] & 0x03) << 8)) * 0.1);
            p.AddValue("Energy buffer", "kWh", "br", (bytes) => buffer = ((bytes[6] >> 2) + ((bytes[7] & 0x03) * 64)) * 0.1);
            /*p.AddValue("SOC", "%", "br", (bytes) => soc = (nominalRemaining - buffer) / (nominalFullPackEnergy - buffer) * 100.0);
             This one seems to be confirmed to be far off the UI displayed SOC
             */

            packets.Add(0x212, p = new Packet(0x212, this));
            p.AddValue("Battery temp", "C", "i",
              (bytes) => ((bytes[7]) / 2.0) - 40.0);

            packets.Add(0x321, p = new Packet(0x321, this));
            p.AddValue("CoolantTempBatteryInlet", "C", "e",
              (bytes) => ((bytes[0] + ((bytes[1] & 0x3) << 8)) * 0.125) - 40);
            p.AddValue("CoolantTempPowertrainInlet", "C", "e",
              (bytes) => (((((bytes[2] & 0xF) << 8) + bytes[1]) >> 2) * 0.125) - 40);
            p.AddValue("Ambient Temp raw", "C", "e",
              (bytes) => ((bytes[3] * 0.5) - 40));
            p.AddValue("Ambient Temp filtered", "C", "e",
              (bytes) => ((bytes[5] * 0.5) - 40));



            packets.Add(0x3D2, p = new Packet(0x3D2, this));
            p.AddValue("Charge total", "kWH", "bs",
                      (bytes) => {
                          chargeTotal =
                      (bytes[0] +
                      (bytes[1] << 8) +
                      (bytes[2] << 16) +
                      (bytes[3] << 24)) / 1000.0;
                    /*if (mainActivity.currentTab.trip.chargeStart == 0)
                      mainActivity.currentTab.trip.chargeStart = chargeTotal;
                    charge = chargeTotal - mainActivity.currentTab.trip.chargeStart;*/
                          return chargeTotal;
                      });

            p.AddValue("Discharge total", "kWH", "b",
                (bytes) => {
                    dischargeTotal =
                (bytes[4] +
                (bytes[5] << 8) +
                (bytes[6] << 16) +
                (bytes[7] << 24)) / 1000.0;
              /*if (mainActivity.currentTab.trip.dischargeStart == 0)
                mainActivity.currentTab.trip.dischargeStart = dischargeTotal;
              discharge = dischargeTotal - mainActivity.currentTab.trip.dischargeStart;*/
                    return dischargeTotal;
                });




            /*packets.Add(0x712, p = new Packet(0x712, this));
            p.AddValue("Last cell block updated", "xb", "", (bytes) => {
              int cell = 0;
              double voltage = 0.0;
              for (int i = 0; i < 3; i++) {
                voltage = (((bytes[i * 2 + 3] << 8) + bytes[i * 2 + 2]) /100.0);
                if (voltage > 0)
                  UpdateItem("Cell " + (cell = ((bytes[0]) * 3 + i + 1)).ToString().PadLeft(2) + " temp"
                    , "zVC"
                    , "z"
                    , bytes[0]
                    , voltage
                    , 0x712);
              }
              return bytes[0];
            });
            */

            // these are placeholders for the filters to be generated correctly.
            p.AddValue("Cell temp min", "C", "b", null);
            p.AddValue("Cell temp avg", "C", "bcp", null);
            p.AddValue("Cell temp max", "C", "b", null);
            p.AddValue("Cell temp diff", "Cd", "bc", null);
            p.AddValue("Cell min", "Vc", "b", null);
            p.AddValue("Cell avg", "Vc", "bpzr", null);
            p.AddValue("Cell max", "Vc", "b", null);
            p.AddValue("Cell diff", "Vcd", "bz", null);
            for (int i = 1; i <= 96; i++)
                p.AddValue("Cell " + i.ToString().PadLeft(2) + " voltage"
                  , "zVC"
                  , "z", null);
            for (int i = 1; i <= 32; i++)
                p.AddValue("Cell " + i.ToString().PadLeft(2) + " temp"
                  , "zCC"
                  , "c"
                  , null);

#endif






        }



    public List<Value> GetAllValues() {
      List<Value> result = new List<TeslaSCAN.Value>();
      foreach (var p in packets)
        foreach (var v in p.Value.values)
          result.Add(v);
      return result;
    }

    internal void LogFast(bool logfast, string path) {
      if (logfast) {
        //string path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        string filename = Path.Combine(path, mainActivity.currentTab.name + " " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".csv");
        fastLogStream = new StreamWriter(filename, true);
        fastLogItems = mainActivity.currentTab.include;
/*#if disablebluetooth
        fastLogItems.Insert(0, new Value("Raw", "", "", null, null));
#endif*/
            string s = "Time,";
        foreach (var i in fastLogItems
         /* .OrderBy(x => x.index)
          .OrderBy(x => x.packetId)*/) {
            s += i.name + separator;
        }
        fastLogStream.WriteLine(s);
        fastLogEnabled = true;
        logTimer = new Stopwatch();
        logTimer.Start();
      }
      else {
        fastLogStream?.Close();
        fastLogEnabled = false;
      }
    }

    public void SaveTrip(string path) {
      string filename = Path.Combine(path, "TripStart " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".xml");
      FileStream output = new FileStream(filename, FileMode.Create);
      XmlSerializer x = new XmlSerializer(typeof(Trip));
      x.Serialize(output, mainActivity.currentTab.trip);
      output.Close();
    }

    public void LoadTrip(string fileName) {
      XmlSerializer mySerializer = new XmlSerializer(typeof(Trip));
      FileStream myFileStream = new FileStream(fileName, FileMode.Open);
      // Call the Deserialize method and cast to the object type.
      mainActivity.currentTab.trip = (Trip)mySerializer.Deserialize(myFileStream);
      myFileStream.Close();
    }

    public void ResetTrip() {
      mainActivity.currentTab.trip = new Trip(false);
    }

    private void ParsePacket(string raw, int id, byte[] bytes) {
      if (packets.ContainsKey(id)) {
        packets[id].Update(bytes);
        numUpdates++;
        if (id == 0x6F2)
          if (bytes[0] >= 24) {
            var values = items.Where(x => x.Value.unit == "zCC");
            double min = values.Min(x => x.Value.GetValue(false));
            double max = values.Max(x => x.Value.GetValue(false));
            double avg = values.Average(x => x.Value.GetValue(false));
            UpdateItem("Cell temp min", "c", "bcz", 1000, min, 0x6F2);
            UpdateItem("Cell temp avg", "c", "bcpz", 1001, avg, 0x6F2);
            UpdateItem("Cell temp max", "c", "bcz", 1002, max, 0x6F2);
            UpdateItem("Cell temp diff", "Cd", "bcz", 1003, max - min, 0x6F2);
          } else {
            var values = items.Where(x => x.Value.unit == "zVC");
            double min = values.Min(x => x.Value.GetValue(false));
            double max = values.Max(x => x.Value.GetValue(false));
            double avg = values.Average(x => x.Value.GetValue(false));
            UpdateItem("Cell min", "Vc", "bz", 1000, min, 0x6F2);
            UpdateItem("Cell avg", "Vc", "bpz", 1001, avg, 0x6F2);
            UpdateItem("Cell max", "Vc", "bz", 1002, max, 0x6F2);
            UpdateItem("Cell diff", "Vcd", "bz", 1003, max - min, 0x6F2);
          }
        if (time < SystemClock.ElapsedRealtime()) {
          UpdateItem("Packets per second", "xp", "", 0, numUpdates, 0xFFF);
          numUpdates = 0;
          time = SystemClock.ElapsedRealtime() + 1000;
          foreach (var item in items.Where(x => x.Value.LimitsChanged()).Select(x => x.Value))
            adapter.Touch(item);
        }
        if (resetGaugeTime < SystemClock.ElapsedRealtime()) {
          adapter.limits.Remove("zCC");
          adapter.limits.Remove("zVC");
          foreach (var item in items.Where(x => x.Value.unit == "zCC" || x.Value.unit == "zVC").Select(x => x.Value))
            item.UpdateLimits(item.GetValue(false));
          resetGaugeTime = SystemClock.ElapsedRealtime() + 1000 * 60;
        }
        if (fastLogEnabled) {
          string s = "";
          int pos = 0, lastPos = 0;
          bool anythingToLog = false;
          foreach (var logItem in fastLogItems) {
            pos++;
            /*if (logItem.name == "Raw")
              s += raw;*/
            if (logItem.packetId!=null && logItem.packetId.Contains(id)) {
              lastPos = pos;
              if (items.ContainsKey(logItem.name)) { // in the case of Front RPM, the next line throws exception on an RWD car (because front RPM is not in items)
                s += items[logItem.name].GetValue(((MainActivity)adapter.GetContext()).convertToImperial)
                  .ToString(System.Globalization.CultureInfo.InvariantCulture);
                anythingToLog = true;
              }
            }
            s += separator;
          }
          if (anythingToLog) 
            fastLogStream.WriteLine(logTimer.ElapsedMilliseconds.ToString() + separator + s);          
        }
      }
    }

    public void UpdateItem(string name, string unit, string tag, int index, double value, int id) {
      ListElement l;
      items.TryGetValue(name, out l);
      if (l == null) {
        items.TryAdd(name, l = new ListElement(name, unit, tag, index, value, adapter, id));
        mainActivity.currentTab.AddElements(l);
        adapter.GetContext().RunOnUiThread(() => {
          adapter.items = mainActivity.currentTab.GetItems(this);
          adapter.NotifyChange();
        });
      } else l.SetValue(value);
      if (l.changed)
        adapter.Touch(l);
    }


    public List<ListElement> GetDefaultItems() {
      return items
        .Values
        .OrderBy(x => x.index)
        .OrderBy(x => { x.selected = false; return x.unit; })
        .ToList<ListElement>();
    }

    public List<ListElement> GetItems(string tag) {
      if (tag=="" || tag==null)
        return GetDefaultItems();
      var charArray = tag.ToCharArray(); // I'll cache it to be nice to the CPU cycles
      tagFilter = charArray;
      return items
        .OrderBy(x => x.Value.index)
        .OrderBy(x => x.Value.unit)
        .Where(x => x.Value.tag?.IndexOfAny(charArray) >= 0)
        .Select(x => { x.Value.selected = false; return x.Value; })
        .ToList();
    }

    public List<Value> GetValues(string tag) {
      var charArray = tag.ToCharArray(); // I'll cache it to be nice to the CPU cycles
      tagFilter = charArray;
      List<Value> values = new List<Value>();
      foreach (var packet in packets)
        foreach (var value in packet.Value.values)
          if (value.tag.IndexOfAny(charArray) >= 0 || tag=="")
            values.Add(value);

      return values
        .OrderBy(x => x.index)
        //.OrderBy(x => x.unit.Trim())
        .ToList();
    }


    public string[] GetCANFilter(List<Value> items) {
      var f=items.FirstOrDefault();
      int filter=0;
      if (f != null)
        filter = f.packetId.First();
      int mask = 0;

      List<int> ids = new List<int>();
      foreach (var item in items)
        foreach (var id in item.packetId)
          if (!ids.Exists(x => x == id))
            ids.Add(id);

      foreach (var id in ids) {
        for (int bit = 0; bit < 11; bit++)
          if (((id >> bit) & 1) != ((filter >> bit) & 1)) {
            mask |= 1 << bit;
            //filter &= ~(1 << bit);
          }
      }
      mask = ~mask & 0x7FF;
      Console.WriteLine(Convert.ToString(mask, 2).PadLeft(11, '0'));
      Console.WriteLine("{0,4} filter: {1,3:X} mask: {2,3:X}", 1, filter, mask, 1, 1);
      List<string> result = new List<string>();
      result.Add(Convert.ToString(mask, 16));
      result.Add(Convert.ToString(filter, 16));
      foreach (int id in ids)
        result.Add(Convert.ToString(id, 16));
      return result.ToArray();
    }

    public string[] GetCANFilter(string tag) {
      int filter = 0;
      int mask = 0;
      List<int> ids = new List<int>();
      foreach (var packet in packets.Values)
        foreach (var value in packet
          .values
          .Where(x => x.tag.IndexOfAny(tag.ToCharArray()) >= 0 || tag==""))
          if (!ids.Exists(x=>x == packet.id))
          ids.Add(packet.id);

      if (tag.Contains('z'))
        ids.Add(0x6F2);     

      foreach (var id in ids) {
        if (filter == 0)
          filter = id;
        for (int bit = 0; bit < 11; bit++)
          if (((id >> bit) & 1) != ((filter >> bit) & 1)) {
            mask |= 1 << bit;
            //filter &= ~(1 << bit);
          }
      }
      mask = ~mask & 0x7FF;
      Console.WriteLine(Convert.ToString(mask, 2).PadLeft(11, '0'));
      Console.WriteLine("{0,4} filter: {1,3:X} mask: {2,3:X}", 1, filter, mask, 1, 1);
      List<string> result = new List<string>();
      result.Add(Convert.ToString(mask, 16));
      result.Add(Convert.ToString(filter, 16));
      foreach (int id in ids)
        result.Add(Convert.ToString(id, 16));
      return result.ToArray();
    }

    // returns true IF startup=true AND all packets tagged with 's' have been received.

    public List<int> GetCANids(string tag) {
      List<int> ids = new List<int>();
      foreach (var packet in packets.Values)
        foreach (var value in packet
          .values
          .Where(x => x.tag.IndexOfAny(tag.ToCharArray()) >= 0 || tag == ""))
          if (!ids.Exists(x => x == packet.id))
            ids.Add(packet.id);
      return ids;
    }


    public bool Parse(string input, int idToFind) {
      if (!input.Contains('\n'))
        return false;
      if (input.StartsWith(">"))
        input = input.Substring(1);
      List<string> lines = input?.Split('\n').ToList();
      lines.Remove(lines.Last());

      bool found = false;

      foreach (var line in lines)
        try {
//          if (!(line.Length == 11 && line.StartsWith("562") || line.StartsWith("232")) &&
//              !(line.Length == 15 && line.StartsWith("116")) &&
//              !(line.Length == 17 && (line.StartsWith("210")||line.StartsWith("115"))) &&
//               line.Length != 19) { // testing an aggressive garbage collector! // 11)
//#if VERBOSE
//            Console.WriteLine("GC " + line);
//#endif
//            continue;
//          }
#if VERBOSE
          Console.WriteLine(line);
#endif
          int id = 0;
          if (!int.TryParse(line.Substring(0,3), System.Globalization.NumberStyles.HexNumber, null, out id))
            continue;
          string[] raw = new string[(line.Length - 3) / 2];
          int r = 0;
          int i;
          for (i = 3; i < line.Length-1; i += 2)
            raw[r++] = line.Substring(i,2);
          List<byte> bytes = new List<byte>();
          i = 0;
          byte b = 0;
          for (i = 0; i < raw.Length; i++)
            if (raw[i].Length != 2 || !byte.TryParse(raw[i], System.Globalization.NumberStyles.HexNumber, null, out b))
              break;
            else bytes.Add(b);
#if disablebluetooth
          if (fastLogEnabled)
            fastLogStream.WriteLine(line);
#endif
          if (bytes.Count == raw.Length) { // try to validate the parsing 
            ParsePacket(line, id, bytes.ToArray());
            MainActivity.bluetoothHandler.ResetTimeout();
            if (idToFind>0)
              if (idToFind == id)
                found=true;
          }
        } catch (Exception e) { Console.WriteLine(e.ToString()); };

      /*if (startup) {
        bool foundAll = true;
        foreach (var p in packets)
          foreach (var v in p.Value.values)
            if (v.tag.Contains('s') &&
            !items.ContainsKey(v.name)) {
              foundAll = false;
              break;
            }
        return foundAll;
      }*/
      if (found) return true;
      return false;
    }


  }
}

