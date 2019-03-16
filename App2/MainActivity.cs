//#define disablebluetooth
/*

Scan My Tesla
By Amund Børsand

battery cell temperature + voltage decoded by EVTV (correct me if I'm wrong?)
all other packets decoded by Jason Hughes (SKIE.NET)

*/


using System;
using Android.App;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Bluetooth;
using System.Linq;
using System.Threading;
using Android.Content;
using System.Collections.Generic;
using Android.Support.V4.App;
using Android.Preferences;
using System.Runtime.Serialization;

namespace TeslaSCAN
{
#if disablebluetooth
  [Activity(
    Label = "scan my tesla DEMO", 
    ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | 
                           Android.Content.PM.ConfigChanges.ScreenSize, 
    MainLauncher = true, Icon = "@drawable/icon")]
#else
  [Activity(
    Label = "scan my tesla", 
    ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | 
                           Android.Content.PM.ConfigChanges.ScreenSize, 
    MainLauncher = true, Icon = "@drawable/icon")]
#endif

  public partial class MainActivity : Activity {

    CustomAdapter ladapter;
    GridView gridView1;
    Parser parser;
    public static BluetoothHandler bluetoothHandler;
    TextView statusText;
    List<ListElement> clipboard = new List<ListElement>();
    public Tab currentTab;
    public Trip trip = new Trip(false);

    public bool convertToImperial=false;

    const int PICKFILE_REQUEST_CODE=500;
    const int PICK_TRIP=600;
    public string filePath;
    private int sel;

    private static int REQUEST_EXTERNAL_STORAGE = 1;
    private static String[] PERMISSIONS_STORAGE = {
        Android.Manifest.Permission.ReadExternalStorage,
        Android.Manifest.Permission.WriteExternalStorage
    };
    private ISharedPreferences prefs;
    private ISharedPreferencesEditor editor;

    [DataMember]
    private List<Tab> tabs;

    private bool starting;
    public bool flagNewTrip;
    private ImageView menuIcon;
    private ImageView prefIcon;

    //private Timer LogTimer;

    string[,] tabTitle = new string[,] {
        {"All","","0","1" },
        {"Perf","p","1","2" },
        {"Temps","c","1","2" },
        {"HVAC","h","1","2" },
        {"Eff","e","1","2" },
        {"Battery","b","0","1" },
        {"BMS","z","1","4" },
        {"Cells","x","1","2" },
        {"Total","t","2","2" },
        {"Trip","t","2","2" }
      };


    public void LogStatus(string s) {
      RunOnUiThread(()=>{
        Console.WriteLine(s);
        statusText.Text += '\n' + s + "  ";
        statusText.Text.Replace("\n\n", "\n");
        statusText.Visibility = Android.Views.ViewStates.Visible;
      });
    }

    public void ClearLog() {
      RunOnUiThread(() => {
        statusText.Visibility = Android.Views.ViewStates.Invisible;
        statusText.Text = "";
      });
    }

    /*private void LogTimerCallback(object state) {
      RunOnUiThread(() => {
        if (statusText.Text.Length>0)
          statusText.Text = statusText.Text.Substring(1);
      });
    }*/

    public void PutPref(string key, string value) {
      var editor = prefs.Edit();
      editor.PutString(key, value);
      editor.Commit();
    }

    protected override void OnCreate(Bundle bundle) {
      base.OnCreate(bundle);

      try {

        starting = true;

      //verifyStoragePermissions();

      Thread.CurrentThread.CurrentCulture =
        System.Globalization.CultureInfo.InvariantCulture;

      //RequestWindowFeature(WindowFeatures.ActionBar);

      //Window.AddFlags(WindowManagerFlags.Fullscreen);
      //ActionBar.SetDisplayShowHomeEnabled(true);

      ActionBar.SetDisplayShowTitleEnabled(false);
      ActionBar.SetDisplayShowHomeEnabled(false);
      ActionBar.NavigationMode = ActionBarNavigationMode.Tabs;

      /*var setHasEmbeddedTabsMethod = ActionBar.Class
                .GetDeclaredMethod("setHasEmbeddedTabs", Java.Lang.Boolean.Type);
      setHasEmbeddedTabsMethod.Accessible=true;
      setHasEmbeddedTabsMethod.Invoke(ActionBar, true);*/

      //ActionBar.MenuVisibility = true;
      //ActionBar.DisplayOptions=ActionBarDisplayOptions
      SetContentView(Resource.Layout.Main);

      gridView1 = FindViewById<GridView>(Resource.Id.gridView1);
      ladapter = new CustomAdapter(this, gridView1);
      gridView1.Adapter = ladapter;

      parser = new Parser(this, ladapter);

      statusText = FindViewById<TextView>(Resource.Id.StatusText);
      /*statusTimer = new System.Timers.Timer(10 * 1000);
      statusTimer.Elapsed += timer_Elapsed;
      statusTimer.AutoReset = false;*/

      statusText.Visibility = Android.Views.ViewStates.Invisible;
      statusText.Text = "";

      // initalize menu
      menuIcon = FindViewById<ImageView>(Resource.Id.menuIcon);
      PopupMenu menu = new PopupMenu(this, menuIcon);
      menu.Inflate(Resource.Menu.OptionsMenu);
      //menu.Menu.FindItem(Resource.Id.selectionMode).SetCheckable(true);

      menuIcon.Click += (s, arg) => {
        menu.Show();
      };
      menuIcon.Clickable = true;
      menu.MenuItemClick += Menu_MenuItemClick;

      prefIcon = FindViewById<ImageView>(Resource.Id.prefIcon);
      PopupMenu prefMenu = new PopupMenu(this, prefIcon);
      prefMenu.Inflate(Resource.Menu.Wrench);
      //menu.Menu.FindItem(Resource.Id.selectionMode).SetCheckable(true);

      prefIcon.Click += (s, arg) => {
        prefMenu.Show();
      };
      prefIcon.Clickable = true;
      prefMenu.MenuItemClick += Menu_MenuItemClick;

      // context menu
      RegisterForContextMenu(gridView1);

      bluetoothHandler = new BluetoothHandler(this, parser);

      tabs = new List<Tab>();

      prefs = PreferenceManager.GetDefaultSharedPreferences(this);

      if (prefs.Contains("tabs")) {
        // read saved stuff
        LoadTabs();
      }
      else
        CreateDefaultTabs();

      convertToImperial=prefs.GetBoolean("convertToImperial", false);
      prefMenu.Menu.FindItem(Resource.Id.metric)
        .SetChecked(!convertToImperial);

      gridView1.Clickable = true;
      gridView1.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) => {
        int packetId = ladapter[e.Position].packetId;
        if (!ladapter[e.Position].selected)
          sel = 1;
        else
          sel++;
        if (sel > 3)
          sel = 0;

        if (sel==0 || sel==1) {
          ladapter[e.Position].selected = sel==1;
          ladapter.Touch(ladapter[e.Position]);
        } else
          foreach (var item in ladapter.items.Where(x => x.packetId == packetId)) {
            item.selected = sel==2;
            ladapter.Touch(item);
          }
        //ladapter.NotifyDataSetChanged();
      };


      var VersionName = PackageManager.GetPackageInfo(PackageName, 0).VersionName;
      //Toast.MakeText(this, "Version " + VersionName, ToastLength.Long).Show();
      prefMenu.Menu.Add("Version " + VersionName);
      var StoredVersion = prefs.GetString("version", "");
      if (StoredVersion!=VersionName) {
          AlertDialog.Builder alert = new AlertDialog.Builder(this);
          alert.SetTitle("Welcome to Scan My Tesla version "+VersionName+"!");

          if (StoredVersion=="" && prefs.All.Keys.Any() ) // if there are any prefs in here, must mean we are upgrading
          alert.SetMessage(                               // but version recording started in 1.3.0, so upgrading from earlier than that
            "This version has some new and redesigned tabs.\n"+
            "Use 'Factory reset all tabs' from the left menu if you want the new layout.\n"+
            "Or, you can select 'New tab' from the right menu to add a single tab from the templates.");
          
          alert.SetCancelable(true);
          alert.SetPositiveButton("Changelog", (senderAlert, args) => {
            var uri = Android.Net.Uri.Parse("https://sites.google.com/view/scanmytesla/changelog");
            var intent = new Intent(Intent.ActionView, uri);
            StartActivity(intent);
          });

          alert.SetNegativeButton("Close", (senderAlert, args) => {            
          });

          Dialog dialog = alert.Create();
          dialog.Show();

          PutPref("version", VersionName);
        }
      

#if !disablebluetooth

      var storedDevice = prefs.GetString("device", "");
      if (storedDevice == "")
        StartActivityForResult(typeof(DeviceListActivity), 1);
      else {
        Intent intent = new Intent(this, typeof(MainActivity));
        intent.PutExtra("device_address", storedDevice);
        OnActivityResult(1, Result.Ok, intent);
      }

      var t = tabs.Where(x => x.name == prefs.GetString("currentTab", "")).FirstOrDefault();
      if (t != null)
        ActionBar.SelectTab(t.ActionBarTab);

      //Finish();


#else

      var t = tabs.Where(x => x.name == prefs.GetString("currentTab", "")).FirstOrDefault();
      if (t!=null)
        ActionBar.SelectTab(t.ActionBarTab);

      bluetoothHandler.Initialize(null);

      Window.SetFlags(WindowManagerFlags.KeepScreenOn, WindowManagerFlags.KeepScreenOn);

      //bluetoothHandler.Start();
#endif
      starting = false;

      verifyStoragePermissions();

        /*if (LogTimer == null)
          LogTimer = new Timer(LogTimerCallback, null, 2000, 200);*/

        //SupportFunctions.Version = context.PackageManager.GetPackageInfo(context.PackageName, 0).VersionName;
        //prefs.GetInt("")

      }
      catch (Exception e) {
        //set alert for executing the task
        AlertDialog.Builder alert = new AlertDialog.Builder(this);
        alert.SetTitle("Error");
        alert.SetMessage(e.ToString());
        alert.SetCancelable(false);
        alert.SetPositiveButton("OK", (senderAlert, args) => {
          System.Environment.Exit(1);
        });

        Dialog dialog = alert.Create();
        dialog.Show();
      }
    }

    private void LoadTabs()
    {
      try {
        string json = prefs.GetString("tabs", "");

        tabs = Tab.DeSerialize(json);

        foreach (var tab in tabs)
          if (tab.name == "All") {
            CreateTab("All", "", "0", "1", tab);
            tab.items = new List<ListElement>();
          } else {

            ActionBar.Tab aTab = ActionBar.NewTab();
            aTab.SetText(tab.name);
            tab.ActionBarTab = aTab;

            Value[] temp = new Value[tab.include.Count];
            tab.include.CopyTo(temp);
            tab.include.Clear();
            foreach (var t in temp)
              try {
                tab.items = new List<ListElement>();
                tab.include.Add(
                  parser
                    .GetAllValues()
                    .Where(x => x.name == t.name)
                    .First());
              }
              catch (Exception) { };

            if (tab.name.Contains("Trip"))
              trip = tab.trip == null ? tab.trip = new Trip(false) : tab.trip;

            aTab.TabSelected += (sender, args) => {
              currentTab = tab;
              if (bluetoothHandler.active)
                bluetoothHandler.ChangeFilter(currentTab.include);
              ladapter.items = currentTab.GetItems(parser);
              ladapter.items.Any(x => x.selected = false);
              gridView1.NumColumns = currentTab.style == 0 ? 1 : currentTab.size;
              gridView1.Invalidate();
              ladapter.NotifyChange();
              if (!starting) {
                editor = prefs.Edit();
                editor.PutString("currentTab", currentTab.name);
                editor.Commit();
              }
            };
            ActionBar.AddTab(aTab);
          }
      }
      catch (Exception e) { Console.WriteLine(e.ToString()); }
    }

    private Tab CreateTab(string name, string tag, string gaugeType, string size, Tab tab=null) {
      ActionBar.Tab aTab = ActionBar.NewTab();
      aTab.SetText(name);

      if (tab == null) {
        tab = new Tab(
          name,
          aTab,
          name == "Total" ? new Trip(true) : trip);

        tabs.Add(tab);
      } else
        tab.ActionBarTab = aTab;
      tab.include = parser.GetValues(tag);
      if (name == "Eff")
        tab.include = 
          tab.include.OrderBy(x => x.unit.Trim()).ToList();
      tab.size = int.Parse(size);
      tab.style = int.Parse(gaugeType);
      if (name == "BMS")
        tab.style = 3;

      aTab.TabSelected += (sender, args) => {
        currentTab = tab;
        if (bluetoothHandler.active)
          bluetoothHandler.ChangeFilter(currentTab.include);
        ladapter.items = currentTab.GetItems(parser);
        ladapter.items.Any(x => x.selected = false);
        gridView1.NumColumns = currentTab.style == 0 ? 1 : currentTab.size;
        gridView1.Invalidate();
        ladapter.NotifyChange();
        if (!starting) {
          editor = prefs.Edit();
          editor.PutString("currentTab", currentTab.name);
          editor.Commit();
        }
      };
      ActionBar.AddTab(aTab);
      return tab;
    }


    private void CreateDefaultTabs() {
      flagNewTrip = true; // to save the first trip

      for (int i = 0; i < tabTitle.GetLength(0); i++) {
        CreateTab(tabTitle[i, 0], tabTitle[i, 1], tabTitle[i, 2], tabTitle[i, 3]);
      }
    }


    public void SaveTabs() {
      RunOnUiThread(() => {
        editor = prefs.Edit();
        string ser = Tab.Serialize(tabs);

        editor.PutString("tabs", ser);

        editor.Commit();
        flagNewTrip = false;
      });
    }


    protected override void OnResume() {
      base.OnResume();
    }


    protected override void OnActivityResult(int requestCode, Result resultCode, Intent data) {
      base.OnActivityResult(requestCode, resultCode, data);
      if (requestCode == PICKFILE_REQUEST_CODE) {
        //ShowDlg(data.DataString+"\n"+data.Type);
        if (data != null) {
          // Put the Uri and MIME type in the result Intent
          /*Intent intentShareFile = new Intent(Intent.ActionSend);
          Java.IO.File fileWithinMyDir = new Java.IO.File(data.DataString);
          //intentShareFile.SetType("application/pdf");
          intentShareFile.PutExtra(Intent.ExtraStream, Android.Net.Uri.Parse( data.DataString));

          intentShareFile.PutExtra(Intent.ExtraSubject,
                              "Sharing File...");
          intentShareFile.PutExtra(Intent.ExtraText, "Sharing File...");

          StartActivity(Intent.CreateChooser(intentShareFile, "Share File"));*/
          Intent mResultIntent = new Intent(Intent.ActionSend);
          //Intent mResultIntent = new Intent(Intent.ActionSendMultiple);
          //mResultIntent.SetDataAndType(
          //                   data.Data,
          //                   "*/*");
          //ContentResolver.GetType(data.Data));
          mResultIntent.SetType("*/*");
          //mResultIntent.ClipData = data.ClipData;
          var list = new List<IParcelable>();
          /*foreach (var c in data.ClipData.ToArray<IParcelable>())
            list.Add(c);
          mResultIntent.PutParcelableArrayListExtra(Intent.ExtraStream, list);
          //mResultIntent.PutExtra(Intent.ExtraStream, data.ClipData);*/
          mResultIntent.PutExtra(Intent.ExtraStream, data.Data);
          mResultIntent.PutExtra(Intent.ExtraSubject, data.Package);

          StartActivity(Intent.CreateChooser(mResultIntent, "titel"));
          // Set the result
          /* MainActivity.this.setResult(Activity.RESULT_OK,
                   mResultIntent);*/
        }/* else {
          mResultIntent.setDataAndType(null, "");
          MainActivity.this.setResult(RESULT_CANCELED,
                  mResultIntent);
        }*/

      } else if (requestCode == PICK_TRIP) {
        parser.LoadTrip(data.DataString);
      } else
        if (resultCode == Result.Ok) {

        //StartService(new Intent(bluetoothHandler, typeof(BluetoothHandler)));

        BluetoothDevice device =
          (from bd in bluetoothHandler.adapter.BondedDevices
           where bd.Address == data.Extras.GetString("device_address")
           select bd).FirstOrDefault();

        try {

          if (device == null) {
            PutPref("device", "");
            throw new Exception("Device " + data.Extras.GetString("device_address") + " not found.");
          }

          PutPref("device", data.Extras.GetString("device_address"));

          Window.SetFlags(WindowManagerFlags.KeepScreenOn, WindowManagerFlags.KeepScreenOn);
          ladapter.items.Clear();
          bluetoothHandler.Initialize(device);

        } catch (Exception e) {
          //set alert for executing the task
          AlertDialog.Builder alert = new AlertDialog.Builder(this);
          alert.SetTitle("Error");
          alert.SetMessage(e.ToString());
          alert.SetCancelable(false);
          alert.SetPositiveButton("OK", (senderAlert, args) => {
            System.Environment.Exit(1);
          });

          Dialog dialog = alert.Create();
          dialog.Show();

        }
      }
    }


    /*public override bool OnCreateOptionsMenu(IMenu menu) {
      MenuInflater.Inflate(Resource.Layout.OptionsMenu, menu);
      optionsMenu = menu;
      return base.OnPrepareOptionsMenu(menu);
    }*/

    public void ChangeTitle(string title) {
      RunOnUiThread(() =>
      ActionBar.Title = title);
    }

    private void Menu_MenuItemClick(object sender, PopupMenu.MenuItemClickEventArgs e) {
      switch (e.Item.ItemId) {
        /*case Resource.Id.selectionMode:
          e.Item.SetChecked(
            selectSingle = !e.Item.IsChecked);
          return;*/
        case Resource.Id.logFast:
          bool logfast;
          e.Item.SetChecked(
            logfast = !e.Item.IsChecked);
          verifyStoragePermissions();
          parser.LogFast(logfast, filePath);
          bluetoothHandler.ChangeFilter(currentTab.include);
          if (logfast)
            ActionBar.NavigationMode = ActionBarNavigationMode.Standard;
          else
            ActionBar.NavigationMode = ActionBarNavigationMode.Tabs;
          return;
        //case Resource.Id.logSlow:
        //e.Item.SetChecked(
        //selectSingle = !e.Item.IsChecked);
        //return;
        case Resource.Id.debugLog:
          bool l;
          e.Item.SetChecked(
             l = !e.Item.IsChecked);
          verifyStoragePermissions();
          bluetoothHandler.Log(l, filePath);
          return;
        /*case Resource.Id.hang:
          e.Item.SetChecked(!e.Item.IsChecked);
          bluetoothHandler.createHangup=!bluetoothHandler.createHangup;
          return;*/
        /*case Resource.Id.verbose:
          e.Item.SetChecked(
            bluetoothHandler.verbose = !e.Item.IsChecked);
          return;*/
        case Resource.Id.metric:
          e.Item.SetChecked(
            ! (convertToImperial = e.Item.IsChecked));
          editor = prefs.Edit();
          editor.PutBoolean("convertToImperial", convertToImperial);
          editor.Commit();
          return;
        case Resource.Id.viewType:
          currentTab.style++;
          if (currentTab.style > 2)
            currentTab.style = 0;
          gridView1.NumColumns = currentTab.style == 0 ? 1 : currentTab.size;
          gridView1.InvalidateViews();
          SaveTabs();
          return;
        case Resource.Id.bigger:
          if (currentTab.style == 0) 
            currentTab.size++;
          else
            currentTab.size--;
          if (currentTab.size < 1)
            currentTab.size = 1;
          gridView1.NumColumns = currentTab.style == 0 ? 1 : currentTab.size;
          SaveTabs();
          return;
        case Resource.Id.smaller:
          if (currentTab.style == 0)
            currentTab.size--;
          else
            currentTab.size++;
          if (currentTab.size < 1)
            currentTab.size = 1;
          gridView1.NumColumns = currentTab.style == 0 ? 1 : currentTab.size;
          SaveTabs();
          return;
        case Resource.Id.bluetoothEnabled:
          bool bt;
          e.Item.SetChecked(
            bt = !e.Item.IsChecked);
          if (bt) {
            bluetoothHandler.Initialize();
            //bluetoothHandler.ChangeFilter(new string(parser.tagFilter));
          } else
            bluetoothHandler.Stop();
          return;
        case Resource.Id.resettrip:
          parser.ResetTrip();
          flagNewTrip = true;
          bluetoothHandler.ChangeFilter(currentTab.include);
          //SaveTabs();
          return;
        case Resource.Id.newtrip:
          //parser.LoadTrip(filePath+ "/TripStart 2017-03-02 21-31-36.xml");
          trip = new Trip(false);
          char c='A';
          for (int i = 0; i < ActionBar.TabCount; i++)
            if (ActionBar.GetTabAt(i).Text.Contains("Trip"))
               c++;
          var newTab=
            CreateTab("Trip "+c, "t", "2", "2");
          flagNewTrip = true;
          ActionBar.SelectTab(newTab.ActionBarTab);
          //SaveTabs();
          return;
        case Resource.Id.newtab:
          PopupMenu menu = new PopupMenu(this, menuIcon);
          menu.Inflate(Resource.Menu.TabsMenu);
          for (int i = 0; i < tabTitle.GetLength(0); i++)
            if (tabTitle[i,0]!="Trip")
              menu.Menu.Add(tabTitle[i, 0]);
          
          menu.MenuItemClick += (s1, arg1) => {
            if (arg1.Item.TitleFormatted.ToString() == "Blank") {
              newTab = CreateTab("New", "0", "0", "1");
              ActionBar.SelectTab(newTab.ActionBarTab);
            } else
              for (int i = 0; i < tabTitle.GetLength(0); i++)
                if (tabTitle[i, 0] == arg1.Item.TitleFormatted.ToString()) {
                  newTab = CreateTab(tabTitle[i, 0], tabTitle[i, 1], tabTitle[i, 2], tabTitle[i, 3]);
                  ActionBar.SelectTab(newTab.ActionBarTab);
                  break;
                }
          };
          menu.Show();
          SaveTabs();
          return;
        case Resource.Id.deletetab:
          //var pos = tabs.IndexOf(currentTab);
          tabs.Remove(currentTab);
          ActionBar.RemoveTab(currentTab.ActionBarTab);
          /*if (pos >= ActionBar.TabCount)
            pos = ActionBar.TabCount-1;
          if (pos < 0)
            pos = 0;
          currentTab = tabs.ElementAtOrDefault(pos);
          ActionBar.SelectTab(currentTab.ActionBarTab);*/
          SaveTabs();
          return;
        case Resource.Id.paste:
          currentTab.include.AddRange(
            from inc in parser.GetAllValues()
            where clipboard.Any(x => x.name == inc.name)
            select inc);
          ladapter.items = currentTab.GetItems(parser);
          ladapter.NotifyChange();
          if (bluetoothHandler.active)
            bluetoothHandler.ChangeFilter(currentTab.include);
          SaveTabs();
          return;
        case Resource.Id.browse:
          //ShowDlg("Logs are stored in\n\n"+filePath);
          Intent intent = new Intent(Intent.ActionGetContent);
          Android.Net.Uri uri = Android.Net.Uri.Parse(filePath);
          //intent.PutExtra(Intent.ExtraAllowMultiple,true);          
          intent.SetDataAndType(uri, "*/*");
          //StartActivity(Intent.CreateChooser(intent, "Logs"));
          /*Intent intent = new Intent(Intent.ActionOpenDocumentTree);*/
          StartActivityForResult(intent, PICKFILE_REQUEST_CODE);
          return;
        case Resource.Id.device:
          StartActivityForResult(typeof(DeviceListActivity), 1);
          return;
        case Resource.Id.resettabs:
          editor = prefs.Edit();
          editor.Remove("tabs");
          editor.Commit();
          Toast.MakeText(this, "Tabs will be reset on app restart.\nTo undo, edit any tab now", ToastLength.Long).Show();
          return;
        case Resource.Id.resetthrow:
          ladapter.limits.Clear();
          foreach (var item in ladapter.items)
            item.UpdateLimits(item.GetValue(false));
          return;
        case Resource.Id.help:
          uri = Android.Net.Uri.Parse("https://sites.google.com/view/scanmytesla/changelog");
          intent = new Intent(Intent.ActionView, uri);
          StartActivity(intent);
          return;
      }
    }

    public override void OnCreateContextMenu(IContextMenu menu, View v, IContextMenuContextMenuInfo menuInfo) {
      if (parser.fastLogEnabled) {
        Toast.MakeText(this, "Items can not be changed while logging", ToastLength.Long).Show();
        return;
      }
      base.OnCreateContextMenu(menu, v, menuInfo);
      MenuInflater.Inflate(Resource.Menu.ContextMenu, menu);
    }

    public override bool OnContextItemSelected(IMenuItem item) {
      switch (item.ItemId) {
        case Resource.Id.copy:
          clipboard.Clear();
          clipboard.AddRange(
            ladapter.items.Where(x => x.selected));
          return true;
        case Resource.Id.delete:
          var ret =
            (from inc in currentTab.include
             where ladapter.items.Any(x => x.name == inc.name && x.selected)
             select inc
             );
          currentTab.include.RemoveAll(x => ret.Contains(x));
          ladapter.items.RemoveAll(x => x.selected);
          ladapter.NotifyDataSetChanged();
          if (bluetoothHandler.active)
            bluetoothHandler.ChangeFilter(currentTab.include);
          SaveTabs();
          return true;
        case Resource.Id.top:
          for (int i = 0; i < ladapter.items.Count; i++) {
            if (ladapter.items[i].selected) {
              if (i < 1)
                continue;
              var inc1 = currentTab.include.Where(x => x.name == ladapter.items[i].name).FirstOrDefault();
              var inc2 = currentTab.include.Where(x => x.name == ladapter.items[i - 1].name).FirstOrDefault();
              var index = currentTab.include.IndexOf(inc2);

              currentTab.include.Remove(inc1);
              currentTab.include.Insert(index, inc1);
            }
          }
          ladapter.items =
            currentTab.items =
            currentTab.GetItems(parser);
          ladapter.NotifyDataSetChanged();
          SaveTabs();

          return true;
        case Resource.Id.bottom:
          for (int i = ladapter.items.Count - 1; i >= 0; i--) {
            if (ladapter.items[i].selected) {
              if (i >= ladapter.items.Count - 1)
                continue;
              var inc1 = currentTab.include.Where(x => x.name == ladapter.items[i].name).FirstOrDefault();
              var inc2 = currentTab.include.Where(x => x.name == ladapter.items[i + 1].name).FirstOrDefault();
              var index = currentTab.include.IndexOf(inc2);

              currentTab.include.Remove(inc1);
              currentTab.include.Insert(index, inc1);
            }
          }
          ladapter.items =
            currentTab.items =
            currentTab.GetItems(parser);
          ladapter.NotifyDataSetChanged();
          SaveTabs();

          return false;
      }
      return base.OnContextItemSelected(item);
    }

    public override void OnBackPressed() {
      base.OnBackPressed();
      bluetoothHandler?.Stop();
      System.Environment.Exit(0);
      //Finish();
    }

    public void ShowDlg(string msg) {
      RunOnUiThread(() => {
        //set alert for executing the task
        AlertDialog.Builder alert = new AlertDialog.Builder(this);
        //alert.SetTitle("Error");
        alert.SetMessage(msg);
        alert.SetCancelable(false);
        alert.SetPositiveButton("OK", (senderAlert, args) => {
          //System.Environment.Exit(1);
        });

        Dialog dialog = alert.Create();
        dialog.Show();
      });
    }


    public void verifyStoragePermissions() {
      try {

        // Check if we have write permission
        var permission = ActivityCompat.CheckSelfPermission(this, Android.Manifest.Permission.WriteExternalStorage);

        if (permission != Android.Content.PM.Permission.Granted) {
          // We don't have permission so prompt the user
          ActivityCompat.RequestPermissions(
                  this,
                  PERMISSIONS_STORAGE,
                  REQUEST_EXTERNAL_STORAGE
          );
        }

        //filePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        //filePath = Android.OS.Environment.ExternalStorageDirectory.Path;
        //filePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        filePath = Android.OS.Environment.GetExternalStoragePublicDirectory("ScanMyTesla").Path;//.ExternalStorageDirectory.Path;
        Java.IO.File f = new Java.IO.File(filePath);
        f.Mkdirs();
      } catch (Exception e) {
        ShowDlg(e.ToString());
      }
    }

  }
}

