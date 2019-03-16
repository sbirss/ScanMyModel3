//#define disablebluetooth
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Bluetooth;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.IO;

namespace TeslaSCAN {

  [Service]
  public partial class BluetoothHandler : Service {

    IBinder mBinder;

    private BluetoothSocket _socket;
    BluetoothDevice device = null;
    public BluetoothAdapter adapter;
    MainActivity mainActivity;
    byte[] buffer = new byte[1024];
    Parser parser;
    public bool active;
    bool stopped = true;
    int recursionDepth;
    bool stCommands = false;
    bool startup = false;
    Thread thread;
    public bool verbose;
    public bool createHangup;
    private StreamWriter streamWriter;
    private bool loggingEnabled;
    Stopwatch runtime = new Stopwatch();
    private bool runRequest;
    private long recieveTime;
    private long parseTime;
    private static Timer timeoutTimer;
    System.IO.Stream inputStream;


#if disablebluetooth
    int pos = 0;
#endif
    public BluetoothHandler() {
      adapter = BluetoothAdapter.DefaultAdapter;
    }

    public BluetoothHandler(MainActivity mainActivity, Parser parser) {
      this.parser = parser;
      this.mainActivity = mainActivity;
      adapter = BluetoothAdapter.DefaultAdapter;
    }

    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId) {
      if (active) {
        Console.WriteLine("BluetoothHandler already running");
      } else {
        Initialize();
      }
      return StartCommandResult.Sticky;
    }

    public void send(string command) {
      try {
        if (loggingEnabled)
          streamWriter.WriteLine(command);
#if !disablebluetooth
        _socket.OutputStream.Write(
          System.Text.Encoding.ASCII.GetBytes(
            command + "\r"), 0, command.Length + 1);
#endif
      } catch (Exception e) {
        mainActivity.LogStatus(e.Message);
      }
    }

    public string receive() {
      try {
        string s = "";
        int x = 10;
        while (!s.Contains('\n') && !s.Contains(">")) {
#if !disablebluetooth
          x = _socket.InputStream.Read(buffer, 0, buffer.Length);
          if (recieveTime < SystemClock.ElapsedRealtime())
            s = "";
          recieveTime = SystemClock.ElapsedRealtime() + 50;
          s += System.Text.Encoding.ASCII.GetString(buffer, 0, x).Replace('\r', '\n');
        }
#else
          /*if (!inputStream.IsDataAvailable())
            inputStream.Seek(0, SeekOrigin.Begin );*/
          s += (char) inputStream.ReadByte();
          if (createHangup)
          while (true) ;
        }
        //Thread.Sleep(1);
#endif
/*#if DEBUG
        Console.WriteLine(s);
#endif*/
        return s;
      } catch (Exception) { return ""; }
    }

    public int sendCommand(string command, string pass=">", string fail="") {
      string s = "";
      int retries = 0;
      //try {
        do {
          if (verbose)
            mainActivity.LogStatus(command);
          _socket?.InputStream.Flush();
          send(command);
          Thread.Sleep(100);
          s = receive();
          if (verbose)
            mainActivity.LogStatus(s);
        } while (!s.Contains(pass) &&
          !(fail != "" && s.Contains(fail)) && retries++<5 );

        if (retries > 5)
          return retries;

        if (fail!="" && s.Contains(fail))
          return -1;
        else return 0; // contains pass

      /*} catch (TimeoutException) {
        mainActivity.LogStatus("Timeout, retrying "+recursionDepth);
        Thread.Sleep(2000);
        recursionDepth++;
        if (recursionDepth < 5)
         return sendCommand(command);
        else {
          recursionDepth = 0;
          return -2; // 
        }
      }*/
    }

    public void Initialize(BluetoothDevice device) {
      this.device = (BluetoothDevice)device;
      Initialize();
    }

    public void Initialize() {
      if (timeoutTimer==null)
        timeoutTimer = new Timer(timeoutCallback, null, 30000, 30000);
      ThreadPool.QueueUserWorkItem(o => InitializeInternal());
    }

    private void timeoutCallback(object state) {
      if (runRequest) {
        mainActivity.ClearLog();
        mainActivity.LogStatus("Timeout");
        ThreadPool.QueueUserWorkItem(o => InitializeInternal());
      }
    }

    public void ResetTimeout() {
       timeoutTimer?.Change(30000, 30000);
    }

    private void InitializeInternal() {
      try {
        createHangup = false;
        Stop();
        runRequest = true;

#if disablebluetooth

        inputStream = mainActivity. Assets.Open("CAN_BUS_LOG_Feb_10.txt");
        //inputStream = mainActivity.Assets.Open("RawLog 2017-01-19 08-32.txt");
        //inputStream = Assets.Open("RawLog 2017-02-02 17-00-34.txt");
        //inputStream = Assets.Open("RawLog 2017-02-19 14-09-07.txt");
        //inputStream = Assets.Open("RawLog 2017-04-19 07-55-34.txt");
        //inputStream = Assets.Open("RawLog 2017-06-04 18-17-36.txt");
        //inputStream = Assets.Open("RawLog 2017-12-03 23-46-09.txt");
        //inputStream = Assets.Open("RawLog.2017-09-17.21-10-56 P100D.txt");
        //inputStream = mainActivity. Assets.Open("RawLog 2018-01-27 15-33-36.txt");
        //inputStream = mainActivity.Assets.Open("RawLog 2018-01-27 15-48-09.txt");

        //inputStream = mainActivity. Assets.Open("RawLog.2017-09-17.21-10-56 P100D.txt");
        //inputStream = mainActivity.Assets.Open("RawLog 2017-02-02 17-00-34.txt"); // fra model X
        //inputStream = Assets.Open("RawLog 2017-01-19 16-30.txt");
        //inputStream = Assets.Open("RawLog 2017-01-19 08-32.txt");
        //inputStream = mainActivity.Assets.Open("RawLog 2017-12-03 23-46-09.txt");
        //inputStream = Assets.Open("RawLog 2017-04-19 07-55-34.txt");
        //inputStream = Assets.Open("RawLog 2017-05-30 16-17-44 kun 210-pakker.txt");
        //inputStream = Assets.Open("RawLog 2017-05-05 15-18-10.txt"); // this one has only battery amps, but with errors!
        //inputStream = Assets.Open("RawLog 2017-05-05 15-06-47.txt"); // this one with errors!
#endif

#if !disablebluetooth
        mainActivity.LogStatus("Creating bluetooth socket...");
        _socket = device.CreateInsecureRfcommSocketToServiceRecord(Java.Util.UUID.FromString("00001101-0000-1000-8000-00805f9b34fb"));
        //_socket = device.CreateRfcommSocketToServiceRecord(Java.Util.UUID.FromString("00001101-0000-1000-8000-00805f9b34fb"));
        mainActivity.LogStatus("Connecting bluetooth socket...");
        _socket.Connect();
#endif
        //_socket.InputStream.CanTimeout = true;
        //_socket.InputStream.ReadTimeout = 1000;
        //await receive();

        startup = true;

        mainActivity.LogStatus("Initializing adapter...");

        //sendCommand("ATWS"); // faster than ATZ

        //sendCommand("ATSP0", "OK"); // set protocol auto
        //sendCommand("ATH1", "OK"); // headers on
        //sendCommand("ATE0", "OK"); // echo off
        //sendCommand("ATS0", "OK"); // spaces off (for speed and buffer lenght)
        //sendCommand("ATCAF0", "OK"); // CAN formatting off

        //int ret = sendCommand("STDI", ">", "?");

        //if (ret == 0)
        //  stCommands = true;

        //if (stCommands)
        //  mainActivity.LogStatus("Using ST1110 command set");
        //else
        //  mainActivity.LogStatus("Using ELM327 command set");

        //if (stCommands)
        //  sendCommand("STM"); // filters wont work until after we have monitored a bit
        //else
        //  sendCommand("ATMA");

        if (mainActivity.trip.dischargeStart == 0) {
          GetTripStartingPoints();
        }

        ChangeFilterInternal(mainActivity.currentTab.include);
        startup = false;
        //parser.SaveTrip(mainActivity.filePath);
        Start();
      } catch (Exception e) {
        mainActivity.LogStatus(e.Message.ToString());
      }
    }

    private void _Start() {
      if (!stopped)
        _Stop();
      if (!runRequest)
        return;

      thread = new Thread(MainLoop);
      thread.Start(0);
    }

    void MainLoop(object packetIdToFind) {
      try {
        string s = "";
        if (!startup)
          mainActivity.ClearLog();

        active = true;
        stopped = false;

        string command =
          stCommands ? "STM" : "ATMA";

        /*if (loggingEnabled && stCommands)
          command="STMA";*/

        //send(command);
        int pos = 0;
        while (active) {
          pos = s.LastIndexOf('\n');
          if (pos >= 0)
            s = s.Substring(pos + 1);
          else s = "";

          if (parseTime < SystemClock.ElapsedRealtime())
            s = "";
          parseTime = SystemClock.ElapsedRealtime() + 50;

          s += receive();

          if (loggingEnabled)
            streamWriter.WriteLine(s);

          if (parser.Parse(s, (int)packetIdToFind) && (int)packetIdToFind>0) 
            active = false;

          if (s.Contains('>')) {
            if (!createHangup)
              send(command);
            s = "";
          }
        }
        stopped = true;
      } catch (Exception e) {
        mainActivity.LogStatus(e.Message);
      }
    }

    private void _Stop() {
      try {
        //mainActivity.LogStatus("Stopping thread...");
        active = false;

        Stopwatch watch = new Stopwatch();
        watch.Start();
        while (stopped == false &&
          watch.ElapsedMilliseconds < 5000) {
        } // wait for thread to quit, or time out after 2 sec

        if (!stopped) // if still not stopped after timeout, try to kill it
          if (thread != null)
            if (thread.IsAlive) {
              mainActivity.LogStatus("Killing thread...");
              stopped = true;
              thread.Suspend();//Abort();//.Suspend();
            }
      } catch (Exception e) { mainActivity.LogStatus(e.Message); }
    }

    public void Stop() {
      runRequest = false;
      _Stop();
    }

    public void Start() {
      runRequest = true;
      _Start();
    }

    public void ChangeTab(object param) {
      bool isTrip=false;
      if (mainActivity.currentTab.name.Contains("Trip") ||
          mainActivity.currentTab.name.Contains("Total")) {
        isTrip = true;
        startup = true;
        GetTripStartingPoints();
        startup = false;
      }
      ChangeFilterInternal(param);
    }

    public void GetTripStartingPoints() {
      List<Value> values = parser.GetValues("s");
      foreach (var value in values) {
        var l = new List<Value>();
        l.Add(value);
        ChangeFilterInternal(l);
        mainActivity.LogStatus("Getting " + value.name + "...");
        MainLoop(value.packetId.First());
      }
      if (mainActivity.flagNewTrip)
        mainActivity.SaveTabs();
    }


    private void ChangeFilterInternal(object param) {
      mainActivity.LogStatus("Setting filters...");
      string[] s;
      if (param is string)
        s = parser.GetCANFilter((string)param);
      else if (param is string[])
        s = (string[])param;
      else 
        // With ELM327, removes the values tagged "i"
        // this was designed to make the trip tabs work
        // it disables the charge counters from updating,
        // narrows the filters so the distance (odometer packet) can be read
        if (!stCommands) { 
        List<Value> newList = new List<Value>();
        foreach (var p in (List<Value>)param)
          if (!p.tag.Contains("i"))
            newList.Add(p);
        s = parser.GetCANFilter(newList);
      } else
        s = parser.GetCANFilter((List<Value>)param);


      //if (stCommands) {
      //  sendCommand("STFCP", "OK");
      //  for (int i = 2; i < s.Length; i++)
      //    sendCommand("STFAP " + s[i].ToUpper().PadLeft(3, '0') + ",7FF", "OK");
      //} else {
      //  sendCommand("ATCM " + s[0].ToUpper().PadLeft(3, '0'), "OK");
      //  sendCommand("ATCF " + s[1].ToUpper().PadLeft(3, '0'), "OK");
      //}

      //int mask = Convert.ToInt32(s[0], 16);
      //mainActivity.ChangeTitle(Convert.ToString(mask, 2).PadLeft(11, '0'));

      if (!startup)
        _Start();
    }


    public void ChangeFilter(object param) {
      if (!startup && runRequest) {
        _Stop();
        var thread = new Thread(new ParameterizedThreadStart(ChangeTab));
        thread.Start(param);
      }
    }


    public void Log(bool enable, string path) {
      if (enable) {
        string filename = Path.Combine(path, "RawLog " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".txt");
        streamWriter = new StreamWriter(filename, true);
        loggingEnabled = true;
        runtime.Reset();
        runtime.Start();
      } else {
        streamWriter?.Close();
        loggingEnabled = false;
      }
    }


    public override IBinder OnBind(Intent intent) {
      return mBinder;
    }


  }
}