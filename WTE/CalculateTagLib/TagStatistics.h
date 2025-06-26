#pragma once

#ifdef _WIN32
#define EXPORT_API __declspec(dllexport)
#else
#define EXPORT_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

struct TagCount {
    int tagId;
    const char* tagName;
    int count;
};

EXPORT_API int __stdcall CalculateUserTagStatistics(
    int timeRange,
    const char* startDate,
    int userId,
    TagCount* outTagCounts,
    int* outSize
);

#ifdef __cplusplus
}
#endif
