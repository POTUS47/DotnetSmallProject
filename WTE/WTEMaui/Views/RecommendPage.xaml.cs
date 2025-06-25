using DataAccessLib.Services;
using Microsoft.Maui.Controls;
using System.ComponentModel;
using System.Threading.Tasks;

namespace WTEMaui.Views
{
    public partial class RecommendPage : ContentPage, INotifyPropertyChanged
    {
        private readonly RecommendService _recommendService;
        private readonly int _userId; // 移除硬编码，改为从当前用户获取
        public string RecommendResult { get; set; } = string.Empty;
        public string StatusMsg { get; set; } = string.Empty;
        public bool HasResult { get; set; } = false;

        public RecommendPage() : this(WTEMaui.App.Services?.GetService<RecommendService>()) { }
        public RecommendPage(RecommendService recommendService)
        {
            InitializeComponent();
            _recommendService = recommendService;
            
            // 从当前登录用户获取用户ID
            _userId = App.CurrentUser?.UserId ?? 1; // 如果未登录，默认使用ID=1
            
            BindingContext = this;
        }

        private async void OnRandomClicked(object sender, EventArgs e)
        {
            StatusMsg = "正在随机推荐...";
            OnPropertyChanged(nameof(StatusMsg));
            var food = await _recommendService.GetRandomFoodAsync(_userId);
            if (food != null)
            {
                RecommendResult = food.Name;
                HasResult = true;
                StatusMsg = string.Empty;
            }
            else
            {
                RecommendResult = string.Empty;
                HasResult = false;
                StatusMsg = "暂无可推荐的食物";
            }
            OnPropertyChanged(nameof(RecommendResult));
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(StatusMsg));
        }

        private async void OnHealthyClicked(object sender, EventArgs e)
        {
            StatusMsg = "正在健康推荐...";
            OnPropertyChanged(nameof(StatusMsg));
            var food = await _recommendService.GetHealthyFoodAsync(_userId);
            if (food != null)
            {
                RecommendResult = food.Name;
                HasResult = true;
                StatusMsg = string.Empty;
            }
            else
            {
                RecommendResult = string.Empty;
                HasResult = false;
                StatusMsg = "暂无可推荐的健康食物";
            }
            OnPropertyChanged(nameof(RecommendResult));
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(StatusMsg));
        }

        public new event PropertyChangedEventHandler PropertyChanged;
        protected new void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}