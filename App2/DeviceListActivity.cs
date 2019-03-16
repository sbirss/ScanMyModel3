/*
* Copyright (C) 2009 The Android Open Source Project
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace TeslaSCAN {

	
	/// <summary>
	/// This Activity appears as a dialog. It lists any paired devices and
	/// devices detected in the area after discovery. When a device is chosen
	/// by the user, the MAC address of the device is sent back to the parent
	/// Activity in the result Intent.
	/// </summary>
	[Activity (Label = "@string/select_device", 
				Theme = "@android:style/Theme.Dialog", 
				ConfigurationChanges=Android.Content.PM.ConfigChanges.KeyboardHidden | Android.Content.PM.ConfigChanges.Orientation)]			
	public class DeviceListActivity : Activity
	{
		// Debugging
		private const string TAG = "DeviceListActivity";
		private const bool Debug = true;
	
		// Return Intent extra
		public const string EXTRA_DEVICE_ADDRESS = "device_address";
	
		// Member fields
		private BluetoothAdapter btAdapter;
		private static ArrayAdapter<string> pairedDevicesArrayAdapter;
		private static ArrayAdapter<string> newDevicesArrayAdapter;
		
		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

      try {

        // Setup the window
        RequestWindowFeature(WindowFeatures.IndeterminateProgress);
        SetContentView(Resource.Layout.device_list);

        // Set result CANCELED incase the user backs out
        SetResult(Result.Canceled);

        // Initialize array adapters. One for already paired devices and
        // one for newly discovered devices
        pairedDevicesArrayAdapter = new ArrayAdapter<string>(this, Resource.Layout.device_name);
        newDevicesArrayAdapter = new ArrayAdapter<string>(this, Resource.Layout.device_name);

        // Find and set up the ListView for paired devices
        var pairedListView = FindViewById<ListView>(Resource.Id.paired_devices);
        pairedListView.Adapter = pairedDevicesArrayAdapter;
        pairedListView.ItemClick += DeviceListClick;

        // Find and set up the ListView for newly discovered devices
        var newDevicesListView = FindViewById<ListView>(Resource.Id.new_devices);
        newDevicesListView.Adapter = newDevicesArrayAdapter;
        newDevicesListView.ItemClick += DeviceListClick;

        var filter = new IntentFilter(BluetoothDevice.ActionFound);

        // Register for broadcasts when discovery has finished
        filter = new IntentFilter(BluetoothAdapter.ActionDiscoveryFinished);

        // Get the local Bluetooth adapter
        btAdapter = BluetoothAdapter.DefaultAdapter;

        // Get a set of currently paired devices
        var pairedDevices = btAdapter.BondedDevices;

        // If there are paired devices, add each one to the ArrayAdapter
        if (pairedDevices.Count > 0) {
          FindViewById<View>(Resource.Id.title_paired_devices).Visibility = ViewStates.Visible;
          foreach (var device in pairedDevices) {
            pairedDevicesArrayAdapter.Add(device.Name + "\n" + device.Address);
          }
        }
        pairedDevicesArrayAdapter.Add("Bluetooth settings");
        pairedDevicesArrayAdapter.Add("Cancel");
      }
      catch (Exception e) { Console.WriteLine(e.ToString()); Finish(); }
		}
		
		protected override void OnDestroy ()
		{
			base.OnDestroy ();
			
			// Make sure we're not doing discovery anymore
			/*if (btAdapter != null) {
				btAdapter.CancelDiscovery();
			}*/
	
			// Unregister broadcast listeners
		}

    /// <summary>
    /// The on-click listener for all devices in the ListViews
    /// </summary>
    void DeviceListClick(object sender, AdapterView.ItemClickEventArgs e) {
      // Cancel discovery because it's costly and we're about to connect
      //btAdapter.CancelDiscovery ();

      // Get the device MAC address, which is the last 17 chars in the View
      var info = (e.View as TextView).Text.ToString();

      if (info == "Bluetooth settings") {
        StartActivityForResult(new Intent(Android.Provider.Settings.ActionBluetoothSettings), 0);
        Finish();
      }

      if (info != "Cancel") {
        var address = info.Substring(info.Length - 17);
        // Create the result Intent and include the MAC address
        Intent intent = new Intent(this, typeof(MainActivity));
        intent.PutExtra(EXTRA_DEVICE_ADDRESS, address);
        // Set result and finish this Activity
        SetResult(Result.Ok, intent);
      }

      //StartActivity(typeof(MainActivity));
      Finish();
    }
		

		
	}
}

