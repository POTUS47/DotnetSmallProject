using LLMLib;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using DataAccessLib.Services;

namespace WTEMaui.Views
{
    public partial class DashboardPage : ContentPage
    {
        private readonly ImageRecognitionService _imageService;
        private readonly OssService _ossService;
        private readonly MealService _mealService;
        private readonly FoodService _foodService;
        private readonly TagService _tagService;
        private byte[] _capturedImageData;
        private string _capturedImagePath;
        private string _ossImagePath;
        private string _currentFoodName = "";
        private string _currentFoodTag = "";
        private readonly ILogger<DashboardPage> _logger;

        private string _currentPreviewImageUrl = "";
        private string _currentPreviewFoodName = "";

        public DashboardPage(
            ImageRecognitionService imageService,
            OssService ossService,
            MealService mealService,
            FoodService foodService,
            TagService tagService,
            ILogger<DashboardPage> logger)
        {
            InitializeComponent();
            _imageService = imageService;
            _ossService = ossService;
            _mealService = mealService;
            _foodService = foodService;
            _tagService = tagService;
            _logger = logger;

            // 初始化UI状态
            LoadingIndicator.IsRunning = false;
            ResultFrame.IsVisible = false;
            PreviewImage.IsVisible = false;
            
            // 初始化历史记录
            _ = LoadMealHistoryAsync();
        }

        private async void OnTakePhotoClicked(object sender, EventArgs e)
        {
            try
            {
                // 重置UI状态
                ResultFrame.IsVisible = false;
                LoadingIndicator.IsRunning = true;

                // 检查权限
                if (!await CheckAndRequestPermissions())
                {
                    await DisplayAlert("权限不足", "需要相机权限才能使用此功能", "确定");
                    return;
                }

                // 拍照
                var photo = await MediaPicker.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = "请拍摄清晰的食物照片"
                });

                if (photo == null) 
                {
                    LoadingIndicator.IsRunning = false;
                    return;
                }

                // 处理选中的照片
                await ProcessSelectedPhotoAsync(photo);
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsRunning = false;
                _logger?.LogError(ex, "拍照失败");
                await DisplayAlert("错误", $"拍照失败: {ex.Message}", "确定");
            }
        }

        /// <summary>
        /// 从相册选择图片
        /// </summary>
        private async void OnSelectFromGalleryClicked(object sender, EventArgs e)
        {
            try
            {
                // 重置UI状态
                ResultFrame.IsVisible = false;
                LoadingIndicator.IsRunning = true;

                // 检查权限
                if (!await CheckAndRequestGalleryPermissions())
                {
                    await DisplayAlert("权限不足", "需要访问相册权限才能使用此功能", "确定");
                    LoadingIndicator.IsRunning = false;
                    return;
                }

                // 从相册选择图片
                var photo = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "请选择食物图片"
                });

                if (photo == null) 
                {
                    LoadingIndicator.IsRunning = false;
                    return;
                }

                // 处理选中的照片
                await ProcessSelectedPhotoAsync(photo);
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsRunning = false;
                _logger?.LogError(ex, "选择图片失败");
                await DisplayAlert("错误", $"选择图片失败: {ex.Message}", "确定");
            }
        }

        /// <summary>
        /// 处理选中的照片（统一处理拍照和相册选择的图片）
        /// </summary>
        private async Task ProcessSelectedPhotoAsync(FileResult photo)
        {
            try
            {
                // 记录照片信息
                _capturedImagePath = photo.FullPath;
                _logger?.LogInformation("照片完整路径: {FullPath}", photo.FullPath);

                // 显示预览
                using (var stream = await photo.OpenReadAsync())
                {
                    _capturedImageData = await ReadFully(stream);
                    _logger?.LogInformation("图片数据大小: {Size} bytes", _capturedImageData.Length);
                    
                    PreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(_capturedImageData));
                    PreviewImage.IsVisible = true;
                }

                // 上传到OSS
                await UploadImageToOssAsync(photo.FullPath, photo.FileName);

                // 自动开始识别
                await StartRecognitionAsync();
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsRunning = false;
                _logger?.LogError(ex, "处理选中照片失败");
                throw;
            }
        }

        /// <summary>
        /// 开始识别食物
        /// </summary>
        private async Task StartRecognitionAsync()
        {
            if (_capturedImageData == null || _capturedImageData.Length == 0)
            {
                await DisplayAlert("提示", "请先拍摄食物照片", "确定");
                return;
            }

            try
            {
                // 调用识别服务
                var result = await _imageService.RecognizeFoodFromImageAsync(_capturedImageData);
                
                // 解析识别结果 (格式: 食物名称/标签)
                ParseRecognitionResult(result);
                
                // 显示结果编辑界面
                UpdateResultDisplay();
                ResultFrame.IsVisible = true;
            }
            catch (Exception ex)
            {
                await DisplayAlert("识别失败", $"识别时出错: {ex.Message}", "确定");
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
            }
        }

        /// <summary>
        /// 解析识别结果
        /// </summary>
        private void ParseRecognitionResult(string result)
        {
            try
            {
                // 假设格式为 "苹果/水果" 或 "苹果"
                if (result.Contains("/"))
                {
                    var parts = result.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    _currentFoodName = parts[0].Trim();
                    _currentFoodTag = parts.Length > 1 ? parts[1].Trim() : "";
                }
                else
                {
                    _currentFoodName = result.Trim();
                    _currentFoodTag = "";
                }
                
                _logger?.LogInformation("解析结果 - 食物名称: {FoodName}, 标签: {FoodTag}", _currentFoodName, _currentFoodTag);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "解析识别结果失败: {Result}", result);
                _currentFoodName = result;
                _currentFoodTag = "";
            }
        }

        /// <summary>
        /// 更新结果显示
        /// </summary>
        private void UpdateResultDisplay()
        {
            FoodNameLabel.Text = string.IsNullOrEmpty(_currentFoodName) ? "未识别到食物" : _currentFoodName;
            FoodTagLabel.Text = string.IsNullOrEmpty(_currentFoodTag) ? "暂无标签" : _currentFoodTag;
            
            FoodNameEntry.Text = _currentFoodName;
            FoodTagEntry.Text = _currentFoodTag;
        }

        // 食物名称编辑相关事件
        private void OnFoodNameTapped(object sender, EventArgs e)
        {
            FoodNameLabel.IsVisible = false;
            FoodNameEntry.IsVisible = true;
            FoodNameEntry.Focus();
        }

        private void OnFoodNameCompleted(object sender, EventArgs e)
        {
            _currentFoodName = FoodNameEntry.Text?.Trim() ?? "";
            FoodNameLabel.Text = string.IsNullOrEmpty(_currentFoodName) ? "未设置食物名称" : _currentFoodName;
            
            FoodNameEntry.IsVisible = false;
            FoodNameLabel.IsVisible = true;
        }

        // 食物标签编辑相关事件
        private void OnFoodTagTapped(object sender, EventArgs e)
        {
            FoodTagLabel.IsVisible = false;
            FoodTagEntry.IsVisible = true;
            FoodTagEntry.Focus();
        }

        private void OnFoodTagCompleted(object sender, EventArgs e)
        {
            _currentFoodTag = FoodTagEntry.Text?.Trim() ?? "";
            FoodTagLabel.Text = string.IsNullOrEmpty(_currentFoodTag) ? "暂无标签" : _currentFoodTag;
            
            FoodTagEntry.IsVisible = false;
            FoodTagLabel.IsVisible = true;
        }

        /// <summary>
        /// 获取或创建食物记录
        /// </summary>
        private async Task<int> GetOrCreateFoodAsync(string foodName)
        {
            try
            {
                return await _foodService.GetOrCreateFoodAsync(foodName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取或创建食物记录失败: {FoodName}", foodName);
                throw new Exception($"处理食物记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取或创建标签记录
        /// </summary>
        private async Task<int> GetOrCreateTagAsync(string tagName)
        {
            try
            {
                return await _tagService.GetOrCreateTagAsync(tagName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取或创建标签记录失败: {TagName}", tagName);
                throw new Exception($"处理标签记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存餐食记录
        /// </summary>
        private async void OnSaveMealClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFoodName))
            {
                await DisplayAlert("提示", "请设置食物名称", "确定");
                return;
            }

            if (string.IsNullOrEmpty(_ossImagePath))
            {
                await DisplayAlert("提示", "图片尚未上传完成，请稍后再试", "确定");
                return;
            }

            try
            {
                LoadingIndicator.IsRunning = true;

                // 获取当前用户ID (这里需要根据实际的用户管理系统获取)
                int currentUserId = 1; // 临时硬编码，实际应该从用户会话中获取

                _logger?.LogInformation("开始保存餐食记录 - 食物: {FoodName}, 标签: {FoodTag}", _currentFoodName, _currentFoodTag);

                // 1. 先检查并创建食物记录
                int foodId = await GetOrCreateFoodAsync(_currentFoodName);
                _logger?.LogInformation("获取到食物ID: {FoodId}", foodId);

                // 2. 如果有标签，检查并创建标签记录
                int? tagId = null;
                if (!string.IsNullOrEmpty(_currentFoodTag))
                {
                    tagId = await GetOrCreateTagAsync(_currentFoodTag);
                    _logger?.LogInformation("获取到标签ID: {TagId}", tagId);
                }

                // 3. 创建餐食记录
                var currentDate = DateOnly.FromDateTime(DateTime.Now);
                var currentTime = TimeOnly.FromDateTime(DateTime.Now);
                var mealType = GetMealTypeByTime(currentTime);
                _logger?.LogInformation("当前餐食类型: {MealType}, 日期: {Date}, 时间: {Time}", mealType, currentDate, currentTime);

                var meal = await _mealService.AddMealAsync(currentUserId, mealType, currentDate, currentTime);
                _logger?.LogInformation("创建餐食记录成功: MealId={MealId}", meal.MealId);

                // 4. 添加食物到餐食
                await _mealService.AddFoodToMealAsync(meal.MealId, foodId, _ossImagePath);
                _logger?.LogInformation("添加食物到餐食成功: MealId={MealId}, FoodId={FoodId}", meal.MealId, foodId);

                // 5. 如果有标签，添加标签到餐食
                if (tagId.HasValue)
                {
                    await _mealService.AddTagToMealFoodAsync(meal.MealId, foodId, tagId.Value);
                    _logger?.LogInformation("添加标签到餐食成功: MealId={MealId}, FoodId={FoodId}, TagId={TagId}", meal.MealId, foodId, tagId);
                }

                // 6. 显示成功消息
                var successMessage = $"餐食记录已保存成功！\n\n" +
                                   $"🍽️ 餐食类型: {mealType}\n" +
                                   $"🥘 食物名称: {_currentFoodName}\n" +
                                   $"🏷️ 食物标签: {(string.IsNullOrEmpty(_currentFoodTag) ? "无" : _currentFoodTag)}\n" +
                                   $"📅 记录时间: {currentDate} {currentTime:HH:mm}";

                await DisplayAlert("保存成功", successMessage, "确定");
                
                // 重置界面
                ResetInterface();
                
                // 刷新历史记录
                await LoadMealHistoryAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存餐食记录失败");
                
                // 提供更详细的错误信息
                var errorMessage = ex.InnerException != null 
                    ? $"{ex.Message}\n详细信息: {ex.InnerException.Message}" 
                    : ex.Message;
                    
                await DisplayAlert("保存失败", $"保存时出错:\n{errorMessage}", "确定");
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
            }
        }

        #region 用餐历史相关方法

        /// <summary>
        /// 加载用餐历史
        /// </summary>
        private async Task LoadMealHistoryAsync()
        {
            try
            {
                _logger?.LogInformation("开始加载用餐历史");
                
                // 获取当前用户ID
                int currentUserId = 1; // 临时硬编码

                // 获取最近7天的用餐记录
                var endDate = DateOnly.FromDateTime(DateTime.Today);
                var startDate = endDate.AddDays(-7);

                var mealHistory = await _mealService.GetUserMealsByDateRangeAsync(currentUserId, startDate, endDate);
                _logger?.LogInformation("从数据库获取到 {Count} 条餐食记录", mealHistory.Count);
                
                // 按时间倒序排列（最新的在上面）
                var sortedHistory = mealHistory
                    .OrderByDescending(m => m.MealDate)
                    .ThenByDescending(m => m.MealTime)
                    .Take(10) // 只显示最近10条记录
                    .ToList();

                _logger?.LogInformation("排序后准备处理 {Count} 条记录", sortedHistory.Count);

                // 为每个餐食记录的图片生成OSS访问URL
                foreach (var meal in sortedHistory)
                {
                    _logger?.LogInformation("处理餐食记录 MealId={MealId}, 包含 {FoodCount} 个食物", 
                        meal.MealId, meal.MealFoodImages?.Count ?? 0);
                    
                    if (meal.MealFoodImages != null)
                    {
                        var updatedMealFoodImages = new List<DataAccessLib.Models.MealFoodImage>();
                        
                        foreach (var mealFoodImage in meal.MealFoodImages)
                        {
                            _logger?.LogInformation("处理食物图片: MealId={MealId}, FoodId={FoodId}, ImagePath={ImagePath}", 
                                mealFoodImage.MealId, mealFoodImage.FoodId, mealFoodImage.ImagePath ?? "null");
                            
                            if (!string.IsNullOrEmpty(mealFoodImage.ImagePath))
                            {
                                try
                                {
                                    // 使用OssService的GetFileUrl方法生成访问URL
                                    var imageUrl = _ossService.GetFileUrl(mealFoodImage.ImagePath, DateTime.Now.AddHours(24));
                                    _logger?.LogInformation("生成OSS URL成功: {ImagePath} -> {ImageUrl}", 
                                        mealFoodImage.ImagePath, imageUrl);
                                    
                                    // 创建一个扩展对象来包含ImageUrl
                                    var extendedMealFoodImage = new MealFoodImageViewModel
                                    {
                                        MealId = mealFoodImage.MealId,
                                        FoodId = mealFoodImage.FoodId,
                                        ImagePath = mealFoodImage.ImagePath,
                                        ImageUrl = imageUrl,
                                        Food = mealFoodImage.Food
                                    };
                                    
                                    updatedMealFoodImages.Add(extendedMealFoodImage);
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogWarning(ex, "生成OSS图片URL失败: {ImagePath}", mealFoodImage.ImagePath);
                                    
                                    // 如果生成URL失败，使用默认的OSS直接访问URL
                                    var defaultUrl = _mealService.GetImageUrl(mealFoodImage.ImagePath);
                                    _logger?.LogInformation("使用默认URL: {ImagePath} -> {DefaultUrl}", 
                                        mealFoodImage.ImagePath, defaultUrl);
                                    
                                    var extendedMealFoodImage = new MealFoodImageViewModel
                                    {
                                        MealId = mealFoodImage.MealId,
                                        FoodId = mealFoodImage.FoodId,
                                        ImagePath = mealFoodImage.ImagePath,
                                        ImageUrl = defaultUrl,
                                        Food = mealFoodImage.Food
                                    };
                                    
                                    updatedMealFoodImages.Add(extendedMealFoodImage);
                                }
                            }
                            else
                            {
                                _logger?.LogInformation("图片路径为空，跳过URL生成: MealId={MealId}, FoodId={FoodId}", 
                                    mealFoodImage.MealId, mealFoodImage.FoodId);
                                
                                // 即使没有图片也要添加到列表中
                                var extendedMealFoodImage = new MealFoodImageViewModel
                                {
                                    MealId = mealFoodImage.MealId,
                                    FoodId = mealFoodImage.FoodId,
                                    ImagePath = mealFoodImage.ImagePath,
                                    ImageUrl = string.Empty,
                                    Food = mealFoodImage.Food
                                };
                                
                                updatedMealFoodImages.Add(extendedMealFoodImage);
                            }
                        }
                        
                        // 替换整个集合
                        meal.MealFoodImages = updatedMealFoodImages;
                        _logger?.LogInformation("餐食 MealId={MealId} 的图片处理完成，共 {Count} 个食物", 
                            meal.MealId, updatedMealFoodImages.Count);
                    }
                }

                _logger?.LogInformation("设置CollectionView数据源，共 {Count} 条记录", sortedHistory.Count);
                MealHistoryCollectionView.ItemsSource = sortedHistory;
                
                // 控制空状态显示
                EmptyStateLayout.IsVisible = !sortedHistory.Any();
                ViewMoreButton.IsVisible = sortedHistory.Any();

                _logger?.LogInformation("加载用餐历史成功，共 {Count} 条记录", sortedHistory.Count);
                
                // 额外调试：检查第一条记录的详细信息
                if (sortedHistory.Any())
                {
                    var firstMeal = sortedHistory.First();
                    _logger?.LogInformation("第一条餐食记录详情: MealId={MealId}, MealType={MealType}, FoodCount={FoodCount}", 
                        firstMeal.MealId, firstMeal.MealType, firstMeal.MealFoodImages?.Count ?? 0);
                    
                    if (firstMeal.MealFoodImages?.Any() == true)
                    {
                        var firstFood = firstMeal.MealFoodImages.First();
                        if (firstFood is MealFoodImageViewModel viewModel)
                        {
                            _logger?.LogInformation("第一个食物详情: FoodName={FoodName}, ImagePath={ImagePath}, ImageUrl={ImageUrl}", 
                                firstFood.Food?.Name ?? "null", 
                                firstFood.ImagePath ?? "null", 
                                viewModel.ImageUrl ?? "null");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载用餐历史失败");
                EmptyStateLayout.IsVisible = true;
                ViewMoreButton.IsVisible = false;
            }
        }

        /// <summary>
        /// 刷新历史记录
        /// </summary>
        private async void OnRefreshHistoryClicked(object sender, EventArgs e)
        {
            try
            {
                RefreshHistoryButton.IsEnabled = false;
                RefreshHistoryButton.Text = "⏳";
                
                await LoadMealHistoryAsync();
                
                // 显示刷新成功提示
                await DisplayAlert("刷新成功", "用餐历史已更新", "确定");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "刷新历史记录失败");
                await DisplayAlert("刷新失败", "无法获取最新数据，请稍后再试", "确定");
            }
            finally
            {
                RefreshHistoryButton.IsEnabled = true;
                RefreshHistoryButton.Text = "🔄";
            }
        }

        /// <summary>
        /// 查看更多历史记录
        /// </summary>
        private async void OnViewMoreHistoryClicked(object sender, EventArgs e)
        {
            try
            {
                // 这里可以导航到一个专门的历史记录页面
                // 或者加载更多数据到当前页面
                
                await DisplayAlert("功能提示", "查看更多历史记录功能待开发\n\n当前显示最近10条记录", "确定");
                
                // TODO: 实现导航到历史记录详情页面
                // await Shell.Current.GoToAsync("//history");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "查看更多历史记录失败");
            }
        }

        /// <summary>
        /// 获取餐食类型对应的图标
        /// </summary>
        private string GetMealTypeIcon(string mealType)
        {
            return mealType switch
            {
                "早饭" => "🌅",
                "午饭" => "☀️",
                "晚饭" => "🌙",
                "零食" => "🍪",
                _ => "🍽️"
            };
        }

        /// <summary>
        /// 格式化餐食时间显示
        /// </summary>
        private string FormatMealTime(DateOnly date, TimeOnly time)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var yesterday = today.AddDays(-1);

            string dateStr = date switch
            {
                var d when d == today => "今天",
                var d when d == yesterday => "昨天",
                _ => date.ToString("MM/dd")
            };

            return $"{dateStr} {time:HH:mm}";
        }

        #endregion

        /// <summary>
        /// 取消操作
        /// </summary>
        private void OnCancelClicked(object sender, EventArgs e)
        {
            ResetInterface();
        }

        /// <summary>
        /// 重置界面
        /// </summary>
        private void ResetInterface()
        {
            ResultFrame.IsVisible = false;
            PreviewImage.IsVisible = false;
            
            _currentFoodName = "";
            _currentFoodTag = "";
            
            CleanupLocalImage();
        }

        /// <summary>
        /// 根据时间判断餐食类型
        /// </summary>
        private string GetMealTypeByTime(TimeOnly time)
        {
            var hour = time.Hour;
            return hour switch
            {
                >= 6 and < 10 => "早饭",
                >= 10 and < 14 => "午饭",
                >= 14 and < 18 => "零食",
                >= 18 and < 24 => "晚饭",
            };
        }

        /// <summary>
        /// 上传图片到OSS
        /// </summary>
        private async Task UploadImageToOssAsync(string localImagePath, string fileName)
        {
            try
            {
                // 生成OSS对象名称
                var fileExtension = Path.GetExtension(fileName);
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var guid = Guid.NewGuid().ToString("N")[..8];
                var ossObjectName = $"food-photos/{DateTime.Now:yyyy/MM/dd}/{timestamp}_{guid}{fileExtension}";

                _logger?.LogInformation("开始上传图片到OSS: {LocalPath} -> {OssPath}", localImagePath, ossObjectName);

                // 在后台线程执行上传
                await Task.Run(() =>
                {
                    _ossService.UploadFile(ossObjectName, localImagePath);
                });

                _ossImagePath = ossObjectName;
                _logger?.LogInformation("图片上传成功: {OssPath}", ossObjectName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "上传图片到OSS失败: {LocalPath}", localImagePath);
                throw;
            }
        }

        /// <summary>
        /// 清理本地缓存图片
        /// </summary>
        private void CleanupLocalImage()
        {
            try
            {
                if (!string.IsNullOrEmpty(_capturedImagePath) && File.Exists(_capturedImagePath))
                {
                    File.Delete(_capturedImagePath);
                    _logger?.LogInformation("已清理本地缓存图片: {Path}", _capturedImagePath);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "清理本地缓存图片失败: {Path}", _capturedImagePath);
            }
            finally
            {
                _capturedImagePath = null;
                _ossImagePath = null;
                _capturedImageData = null;
            }
        }

        /// <summary>
        /// 检查和请求相册权限
        /// </summary>
        private async Task<bool> CheckAndRequestGalleryPermissions()
        {
            try
            {
                var mediaStatus = await Permissions.CheckStatusAsync<Permissions.Media>();
                if (mediaStatus != PermissionStatus.Granted)
                {
                    mediaStatus = await Permissions.RequestAsync<Permissions.Media>();
                }

                // 对于较低版本的Android，可能需要检查Photos权限
                var photosStatus = await Permissions.CheckStatusAsync<Permissions.Photos>();
                if (photosStatus != PermissionStatus.Granted)
                {
                    photosStatus = await Permissions.RequestAsync<Permissions.Photos>();
                }

                return mediaStatus == PermissionStatus.Granted || photosStatus == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "相册权限检查失败");
                return false;
            }
        }

        private async Task<bool> CheckAndRequestPermissions()
        {
            try
            {
                var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                }
                return cameraStatus == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "权限检查失败");
                return false;
            }
        }

        private async Task<byte[]> ReadFully(Stream input)
        {
            using var ms = new MemoryStream();
            await input.CopyToAsync(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// 扩展的MealFoodImage视图模型，包含ImageUrl属性
        /// </summary>
        public class MealFoodImageViewModel : DataAccessLib.Models.MealFoodImage
        {
            public string ImageUrl { get; set; } = string.Empty;
        }

        #region 图片预览相关方法

        /// <summary>
        /// 缩略图点击事件
        /// </summary>
        private async void OnThumbnailTapped(object sender, EventArgs e)
        {
            try
            {
                if (sender is Frame frame && frame.BindingContext is MealFoodImageViewModel viewModel)
                {
                    if (!string.IsNullOrEmpty(viewModel.ImageUrl))
                    {
                        await ShowImagePreviewAsync(viewModel.ImageUrl, viewModel.Food?.Name ?? "食物图片");
                    }
                    else
                    {
                        await DisplayAlert("提示", "该食物暂无图片", "确定");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "显示图片预览失败");
                await DisplayAlert("错误", "无法显示图片预览", "确定");
            }
        }

        /// <summary>
        /// 显示图片预览
        /// </summary>
        private async Task ShowImagePreviewAsync(string imageUrl, string foodName)
        {
            try
            {
                _currentPreviewImageUrl = imageUrl;
                _currentPreviewFoodName = foodName;
                
                // 设置标题和图片
                PreviewTitle.Text = foodName;
                PreviewInfo.Text = "正在加载图片...";
                
                // 加载图片
                PopupPreviewImage.Source = new UriImageSource
                {
                    Uri = new Uri(imageUrl),
                    CachingEnabled = true,
                    CacheValidity = TimeSpan.FromDays(1)
                };
                
                // 显示弹窗
                ImagePreviewOverlay.IsVisible = true;
                
                // 添加渐入动画
                ImagePreviewOverlay.Opacity = 0;
                await ImagePreviewOverlay.FadeTo(1, 300);
                
                PreviewInfo.Text = "点击空白处关闭";
                
                _logger?.LogInformation("显示图片预览: {FoodName} - {ImageUrl}", foodName, imageUrl);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载预览图片失败: {ImageUrl}", imageUrl);
                PreviewInfo.Text = "图片加载失败";
            }
        }

        /// <summary>
        /// 关闭预览弹窗
        /// </summary>
        private async void OnClosePreviewClicked(object sender, EventArgs e)
        {
            await CloseImagePreviewAsync();
        }

        /// <summary>
        /// 点击遮罩层关闭预览
        /// </summary>
        private async void OnOverlayTapped(object sender, EventArgs e)
        {
            await CloseImagePreviewAsync();
        }

        /// <summary>
        /// 关闭图片预览
        /// </summary>
        private async Task CloseImagePreviewAsync()
        {
            try
            {
                // 添加渐出动画
                await ImagePreviewOverlay.FadeTo(0, 200);
                ImagePreviewOverlay.IsVisible = false;
                
                // 清理资源
                PopupPreviewImage.Source = null;
                _currentPreviewImageUrl = "";
                _currentPreviewFoodName = "";
                
                _logger?.LogInformation("关闭图片预览");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "关闭图片预览失败");
            }
        }

        /// <summary>
        /// 查看原图
        /// </summary>
        private async void OnViewOriginalClicked(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentPreviewImageUrl))
                {
                    // 在浏览器中打开原图
                    await Browser.OpenAsync(_currentPreviewImageUrl, BrowserLaunchMode.SystemPreferred);
                    _logger?.LogInformation("在浏览器中打开原图: {ImageUrl}", _currentPreviewImageUrl);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "打开原图失败: {ImageUrl}", _currentPreviewImageUrl);
                await DisplayAlert("错误", "无法打开原图", "确定");
            }
        }

        /// <summary>
        /// 处理返回键或手势（可选）
        /// </summary>
        protected override bool OnBackButtonPressed()
        {
            if (ImagePreviewOverlay.IsVisible)
            {
                _ = CloseImagePreviewAsync();
                return true; // 拦截返回键事件
            }
            return base.OnBackButtonPressed();
        }

        #endregion
    }
}