using Android;
using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Graphics.Drawables.Shapes;
using Android.OS;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace TeslaSCAN {


  public class CustomAdapter : BaseAdapter<ListElement> {

    public List<ListElement> items;
    GridView gridView;
    public Dictionary<string, ValueLimit> limits;

    public Activity GetContext() {
      return context;
    }

    public void NotifyChange() {
      //NotifyDataSetChanged();
      context.RunOnUiThread(() => NotifyDataSetChanged());
    }

    public void Touch(ListElement l) {
      context.RunOnUiThread(() => {
        var currentTime = SystemClock.ElapsedRealtime();
        if (l.timeStamp > currentTime)
          return;

        l.timeStamp = currentTime+ 1000 / 60; // we are guessing 60 fps is max

        int pos = items.IndexOf(l);
        if (pos >= this.gridView.FirstVisiblePosition
            && pos <= this.gridView.LastVisiblePosition)
          GetView(
            pos,
            this.gridView.GetChildAt(pos - this.gridView.FirstVisiblePosition),
            gridView);
      });
    }

    Activity context;

    public CustomAdapter(Activity context, GridView gridView)
        : base() {
      this.context = context;
      this.items = new List<ListElement>();
      this.gridView = gridView;
      limits = new Dictionary<string, ValueLimit>();
    }

    public override long GetItemId(int position) {
      return position;
    }
    public override ListElement this[int position]
    {
      get { return items.ElementAt(position); }
    }
    public override int Count
    {
      get { return items.Count; }
    }

    public override View GetView(int position, View convertView, ViewGroup parent) {
      var item = items.ElementAt(position);
      View view = convertView;
      if (item.unit == "zVC" || item.unit == "zCC")
        item.viewType = 1;
      else
        item.viewType = ((MainActivity)context).currentTab.style*2;
      if (((MainActivity)context).currentTab.style == 3)
        item.viewType = 5;
      if (view == null ||
         (int)view.Tag != item.viewType) { // no view to re-use, create new

        if (item.viewType == 0)
          view = context.LayoutInflater.Inflate(Resource.Layout.ListItem, null);
        if (item.viewType == 1)
          //view = context.LayoutInflater.Inflate(Resource.Layout.ListItemBattery, null);
          view = context.LayoutInflater.Inflate(Resource.Layout.ListItem2, null);
        if (item.viewType == 2 || item.viewType == 4)
          view = context.LayoutInflater.Inflate(Resource.Layout.ListItemGauge, null);
        //view.FindViewById<TextView>(Resource.Id.textView2).SetHeight(view.Width);
        //view.LayoutParameters.Height = view.FindViewById(Resource.Id.relativeLayout1).Width;
        if (item.viewType == 5)
          view = context.LayoutInflater.Inflate(Resource.Layout.ListItemBattery, null);

        item.changed = true;
      }


      //DrawArc();

      try {

        bool convertToImperial = ((MainActivity)context).convertToImperial;

        double val = item.GetValue(convertToImperial);
        if (double.IsNaN(val))
          return view;
        string str;

        if (item.viewType != 5)
          switch (item.selected) {
            case false: view.FindViewById<TextView>(Resource.Id.textView1).Text = item.name; break;
            case true:
              view.FindViewById<TextView>(Resource.Id.textView1).Text = item.name +
          " (" + item.GetMin(convertToImperial).ToString("0.0") + "/" + item.GetMax(convertToImperial).ToString("0.0") + ")"; break;
          } else
          if (item.index < 2000) {
          view.FindViewById<TextView>(Resource.Id.textView4).Text = item.name;
          view.FindViewById<TextView>(Resource.Id.textView4).Visibility = ViewStates.Visible;
        } else
        if ((position / 4) % 2 == 0) {
          if (position % 4 == 0)
            view.FindViewById<TextView>(Resource.Id.textView4).Text = "Module " + ((item.index - 2000) / 6 + 1);
          else
            view.FindViewById<TextView>(Resource.Id.textView4).Text = "";
          view.FindViewById<TextView>(Resource.Id.textView4).Visibility = ViewStates.Visible;
        } else view.FindViewById<TextView>(Resource.Id.textView4).Visibility = ViewStates.Gone;



        if (item.unit.ToUpper().Contains("VC"))
          str = "0.000";
        else if (Math.Abs(val) < 10)
          str = "0.00";
        else if (Math.Abs(val) < 100)
          str = "0.0";
        else str = "0";

        var progress = view.FindViewById<ProgressBar>(Resource.Id.ProgressBar1);

        view.FindViewById<TextView>(Resource.Id.textView2).Text = val.ToString(str);
        if (item.viewType != 5 &&
            item.viewType != 1)
          view.FindViewById<TextView>(Resource.Id.textView3).Text = item.GetUnit(convertToImperial);

        var min = item.GetGlobalMin(convertToImperial);
        var max = item.GetGlobalMax(convertToImperial);
        var span = -min + max;
        var zero = min;
        if (min < 0)
          zero = 0;

        if (item.viewType == 5) {
          view.FindViewById<TextView>(Resource.Id.textView2).Text += " " + item.GetUnit(convertToImperial);
          if (val == items.ElementAt(0).GetValue(convertToImperial) ||
              val == items.ElementAt(4).GetValue(convertToImperial))
            view.FindViewById<TextView>(Resource.Id.textView2).SetTextColor(Color.Blue);
          else 
          if (val == items.ElementAt(2).GetValue(convertToImperial) ||
              val == items.ElementAt(6).GetValue(convertToImperial))
            view.FindViewById<TextView>(Resource.Id.textView2).SetTextColor(Color.Red);
          else
            view.FindViewById<TextView>(Resource.Id.textView2).SetTextColor(Color.LightGray);
        }

        if (item.viewType == 0) {
          view.FindViewById<TextView>(Resource.Id.textView2).SetTextSize(Android.Util.ComplexUnitType.Pt, ((MainActivity)context).currentTab.size*10);
          progress.LayoutParameters.Height = view.FindViewById<TextView>(Resource.Id.textView2).Height-2;
            //((MainActivity)context).currentTab.size * 50;
        }


        if (item.viewType > 1 && item.viewType < 5 && span > 0) {

          ArcView g = (ArcView) view.FindViewById(Resource.Id.arcView1);
          if (item.selected)
            view.SetBackgroundColor(Color.ParseColor("#30FF8800"));
          else
            view.SetBackgroundColor(Color.Black);

          if (item.viewType == 4)
            g.Visibility = ViewStates.Invisible;
          else g.Visibility = ViewStates.Visible;

            if (val >= 0)
              g.angle =
                (float)((val - zero) / (max - zero) * 180.0);
            else
              g.angle =
                (float)((val - zero) / (max - zero) * 180.0) + 180;

            g.negative = val < 0;
          
            g.Invalidate();
        }


        if (progress != null) {

          progress.Invalidate();

          if (val < 0) {
            progress.Rotation = 180;

            if (item.selected)
              progress.ProgressDrawable.SetColorFilter(Color.ParseColor("#30FF8800"), PorterDuff.Mode.Add);
            else
              progress.ProgressDrawable.SetColorFilter(Color.ParseColor("#FF007700"), PorterDuff.Mode.Overlay);

            progress.Max = Convert.ToInt32(-min * 1000);
            progress.Progress = Convert.ToInt32(-val * 1000);

          } else {

            progress.Rotation = 0;

            if (item.selected)
              progress.ProgressDrawable.SetColorFilter(Color.ParseColor("#30FF8800"), PorterDuff.Mode.Add);
            else
              progress.ProgressDrawable.ClearColorFilter();

            progress.Max = Convert.ToInt32((max - zero) * 1000);
            progress.Progress = Convert.ToInt32((val - zero) * 1000);
          }
        }
        view.Tag=item.viewType;
        item.changed = false;

        return view;
      } catch (Exception e) { Console.WriteLine(e.Message); return view; };
    }
  }
}
