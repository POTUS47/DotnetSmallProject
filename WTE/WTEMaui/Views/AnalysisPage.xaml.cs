using DataAccessLib.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace WTEMaui.Views
{
    public partial class AnalysisPage : ContentPage
    {
        public ObservableCollection<DailyStatVM> DailyStats { get; set; } = new();
        public string UserName { get; set; } = "POTUS 47";
        public string HealthAdvice { get; set; } = "健康建议加载中...";

        private readonly AnalysisService _analysisService;
        private readonly int _userId = 1; // TODO: 替换为实际登录用户ID
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
                BindingContext = this;
                _startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-14));
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