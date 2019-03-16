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

namespace TeslaSCAN {

  [DataContract]
  public class Trip {
    [DataMember]
    public double odometerStart;
    [DataMember]
    public double chargeStart;
    [DataMember]
    public double dischargeStart;
    [DataMember]
    public double acChargeStart;
    [DataMember]
    public double dcChargeStart;

    public Trip(bool totals) {
      if (totals) {
        odometerStart = 0.000001;
        chargeStart = 0.000001;
        dischargeStart = 0.000001;
        acChargeStart = 0.000001;
        dcChargeStart = 0.000001;
      }
    }
  }
}