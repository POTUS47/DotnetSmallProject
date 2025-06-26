#include "pch.h"
#include "TagStatistics.h"
#include <unordered_map>
#include <cstring>
#include <vector>
#include <algorithm>

int __stdcall CalculateUserTagStatistics(
    int timeRange,
    const char* startDate,
    int userId,
    TagCount* outTagCounts,
    int* outSize
) {
    try {
        if (!outTagCounts || !outSize || !startDate) {
            return -1; // 参数错误
        }

        // 数据已经在C#端准备好，这里只需要进行必要的处理
        // 例如：可以在这里添加额外的统计逻辑，过滤，排序等

        // 这里示例：按照count降序排序
        std::vector<TagCount> tags(*outSize);
        for (int i = 0; i < *outSize; i++) {
            tags[i] = outTagCounts[i];
        }

        std::sort(tags.begin(), tags.end(), 
            [](const TagCount& a, const TagCount& b) {
                return a.count > b.count;
            });

        // 将排序后的结果复制回输出数组
        for (int i = 0; i < *outSize; i++) {
            outTagCounts[i] = tags[i];
        }

        return 0; // 成功
    }
    catch (...) {
        return -2; // 未知错误
    }
}
