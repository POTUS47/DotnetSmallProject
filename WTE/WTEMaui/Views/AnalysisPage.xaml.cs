using DataAccessLib.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Syncfusion.Maui.Calendar;
using System.ComponentModel;
using System.Globalization;

namespace WTEMaui.Views
{
    public partial class AnalysisPage : ContentPage, INotifyPropertyChanged
    {
        public ObservableCollection<DailyStatVM> DailyStats { get; set; } = new();
        public string UserName { get; set; } = "POTUS 47";
        public string HealthAdvice { get; set; } = "健康建议加载中...";

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate != value)
                {
                    _selectedDate = value;
                    OnPropertyChanged(nameof(SelectedDate));
                    UpdateSelectedDayFoods();
                }
            }
        }

        public ObservableCollection<string> SelectedDayFoods { get; set; } = new();

        private readonly AnalysisService _analysisService;
        private readonly int _userId; // 移除硬编码，改为从当前用户获取
        private DateOnly _startDate;
        private DateOnly _endDate;

        public AnalysisPage() : this(WTEMaui.App.Services?.GetService<DataAccessLib.Services.AnalysisService>())
        {
        }

        public AnalysisPage(AnalysisService analysisService)
        {
            try
            {
                InitializeComponent();
                _analysisService = analysisService;
                
                // 从当前登录用户获取用户ID
                _userId = App.CurrentUser?.UserId ?? 1; // 如果未登录，默认使用ID=1
                
                // 设置用户名显示
                UserName = App.CurrentUser?.Username ?? "未登录用户";
                
                BindingContext = this;

                // 设置日历的标识符
                FoodCalendar.Identifier = CalendarIdentifier.Gregorian;

                _startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
                _endDate = DateOnly.FromDateTime(DateTime.Today);
                LoadData();
            }
            catch (Exception ex)
            {
                Application.Current?.MainPage?.DisplayAlert("分析页初始化异常", ex.ToString(), "确定");
                throw;
            }
        }

        private async void LoadData()
        {
            var stats = await _analysisService.GetUserDailyStatsAsync(_userId, _startDate, _endDate);
            DailyStats.Clear();
            foreach (var s in stats)
            {
                DailyStats.Add(new DailyStatVM
                {
                    Date = s.Date.ToString("yyyy-MM-dd"),
                    FoodsString = s.Foods.Count > 0 ? string.Join(", ", s.Foods) : "无记录",
                    TotalCalories = s.TotalCalories,
                    TotalProtein = s.TotalProtein
                });
            }
            HealthAdvice = await _analysisService.GetHealthAdviceAsync(_userId, _startDate, _endDate);
            OnPropertyChanged(nameof(HealthAdvice));
            UpdateSelectedDayFoods();
        }

        private void UpdateSelectedDayFoods()
        {
            SelectedDayFoods.Clear();
            var dateStr = SelectedDate.ToString("yyyy-MM-dd");
            var stat = DailyStats.FirstOrDefault(d => d.Date == dateStr);
            if (stat != null && stat.FoodsString != "无记录")
            {
                foreach (var food in stat.FoodsString.Split(", "))
                {
                    SelectedDayFoods.Add(food);
                }
            }
            else
            {
                SelectedDayFoods.Add("暂无数据");
            }
        }

        private void OnCalendarSelectionChanged(object sender, CalendarSelectionChangedEventArgs e)
        {
            if (e.NewValue is DateTime date)
            {
                SelectedDate = date;
            }
        }

        public new event PropertyChangedEventHandler PropertyChanged;
        protected new void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class DailyStatVM
        {
            public string Date { get; set; }
            public string FoodsString { get; set; }
            public double TotalCalories { get; set; }
            public double TotalProtein { get; set; }
        }
    }
}