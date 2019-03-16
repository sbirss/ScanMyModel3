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
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;

namespace TeslaSCAN {

  [DataContract]
  public class Tab {
    [DataMember]
    public string name;
    [DataMember]
    public List<Value> include=new List<Value>();
    
    public List<ListElement> items=new List<ListElement>();
    [DataMember]
    public int size=1;
    [DataMember]
    public int style = 0; // 0=bar/text, 1=gauge, 2=battery block
    [DataMember]
    public Trip trip;

    public ActionBar.Tab ActionBarTab { get; internal set; }

    public Tab(string name, ActionBar.Tab actionBarTab, Trip trip) {
      this.name = name;
      this.trip = trip;
      this.ActionBarTab = actionBarTab;
      if (name.Contains("Trip"))
        this.size = 2;
    }

    public void AddElements(List<ListElement> list) {
      foreach (var l in list)
        if (include.Any(x => x.name == l.name))
          items.Add(l);
    }

    public void AddElements(ListElement element) {
      if (include.Any(x => x.name == element.name))
        items.Add(element);
    }

    public List<ListElement> GetItems(Parser p) {
      return
      (from o in include
         join item in p.items.Values
         on o.name equals item.name
         select item)
          .ToList();
    }

    public static string Serialize(List<Tab> list) {
      var ser = new DataContractJsonSerializer(typeof(List<Tab>));
      MemoryStream m = new MemoryStream();
      ser.WriteObject(m, list);

      m.Position = 0;
      StreamReader sr = new StreamReader(m);
      //Console.Write("JSON form of Person object: ");
      //Console.WriteLine(sr.ReadToEnd());

      return sr.ReadToEnd();
    }

    public static List<Tab> DeSerialize(string json) {
      List<Tab> tabs = new List<Tab>();
      MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
      DataContractJsonSerializer ser = new DataContractJsonSerializer(tabs.GetType());
      tabs= ser.ReadObject(ms) as List<Tab>;
      ms.Close();
      return tabs;
    }

    /* public void TabSelected(object sender, ActionBar.TabEventArgs e) {
         if (bluetoothHandler.active)
           bluetoothHandler.ChangeFilter(tabTitle[i, 1]);
         ladapter.items = t.GetItems(parser);
         ladapter.NotifyChange();
       };
     }*/
  }

}