using DataAccessLib.Services;
using LLMLib;
using Microsoft.Maui.Controls;
using Syncfusion.Maui.Calendar;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace WTEMaui.Views
{
    public partial class AnalysisPage : ContentPage, INotifyPropertyChanged
    {
        public ObservableCollection<DailyStatVM> DailyStats { get; set; } = new();
        public string UserName { get; set; } = "POTUS 47";
        
        private string _healthAdvice = "";
        public string HealthAdvice
        {
            get => _healthAdvice;
            set
            {
                if (_healthAdvice != value)
                {
                    _healthAdvice = value;
                    OnPropertyChanged(nameof(HealthAdvice));
                    OnPropertyChanged(nameof(ShowHealthAdvice));
                }
            }
        }

        private bool _isLoadingAdvice = false;
        public bool IsLoadingAdvice
        {
            get => _isLoadingAdvice;
            set
            {
                if (_isLoadingAdvice != value)
                {
                    _isLoadingAdvice = value;
                    OnPropertyChanged(nameof(IsLoadingAdvice));
                    OnPropertyChanged(nameof(ShowGetAdviceButton));
                }
            }
        }

        public bool ShowGetAdviceButton => !IsLoadingAdvice && string.IsNullOrEmpty(HealthAdvice);
        public bool ShowHealthAdvice => !string.IsNullOrEmpty(HealthAdvice);

        private readonly MealService _mealService;
        private readonly HealthAnalysisService _healthAnalysisService;

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


        public AnalysisPage(HealthAnalysisService healthAnalysisService,MealService mealService, AnalysisService analysisService)
        {
            try
            {
                InitializeComponent();
                _analysisService = analysisService;
                
                _userId = App.CurrentUser?.UserId ?? 1; // 如果未登录，默认使用ID=1
                UserName = App.CurrentUser?.Username ?? "未登录用户";
                BindingContext = this;
                _mealService = mealService;
                _healthAnalysisService= healthAnalysisService;
                FoodCalendar.Identifier = CalendarIdentifier.Gregorian;

                _startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
                _endDate = DateOnly.FromDateTime(DateTime.Today);
                Task.Run(async () =>
                {
                    await LoadData();
                });
            }
            catch (Exception ex)
            {
                Application.Current?.MainPage?.DisplayAlert("分析页初始化异常", ex.ToString(), "确定");
                throw;
            }
        }

        private async void OnGetAdviceClicked(object sender, EventArgs e)
        {
            IsLoadingAdvice = true;
            try
            {
                await AnalysisLLM();
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"获取健康建议失败: {ex.Message}", "确定");
                HealthAdvice = "获取健康建议失败，请稍后重试";
            }
            finally
            {
                IsLoadingAdvice = false;
            }
        }

        private async Task AnalysisLLM()
        {
            var history = await _mealService.GetUserMealsJsonAsync(_userId);
            try
            {
                Console.WriteLine("正在调用大模型分析饮食数据...");

                // ✅ 调用分析方法
                string advice = await _healthAnalysisService.AnalyzeMealTimeAsync(history);
                HealthAdvice = advice;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                throw; // 重新抛出异常让调用方处理
            }
        }

        private async Task LoadData()
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

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(SettingsPage));
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", "无法打开设置页面", "确定");
            }
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