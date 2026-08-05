#if defined(_WIN32)
#  define WIN32_LEAN_AND_MEAN 1
#  include <Windows.h>
#else
#  include <unistd.h>
#  include <thread>
#endif

#include <rpp/thread_pool.h>

#ifndef DLLEXPORT
#  if defined(_MSC_VER)
#    define DLLEXPORT extern "C" __declspec(dllexport)
#  else
#    define DLLEXPORT extern "C" __attribute__((visibility("default")))
#  endif
#endif
#ifndef SD_CALL
#  if defined(_MSC_VER)
#    define SD_CALL __stdcall
#  else
#    define SD_CALL
#  endif
#endif

DLLEXPORT int SD_CALL GetPhysicalCPUCoreCount()
{
    static int num_cores = []
    {
#if defined(_WIN32)
        DWORD bytes = 0;
        GetLogicalProcessorInformation(nullptr, &bytes);
        std::vector<SYSTEM_LOGICAL_PROCESSOR_INFORMATION> coreInfo(bytes / sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION));
        GetLogicalProcessorInformation(coreInfo.data(), &bytes);

        int cores = 0;
        for (auto& info : coreInfo)
        {
            if (info.Relationship == RelationProcessorCore)
                ++cores;
        }
        return cores > 0 ? cores : 1;
#else
        long n = sysconf(_SC_NPROCESSORS_ONLN);
        if (n < 1)
            n = static_cast<long>(std::thread::hardware_concurrency());
        return n > 0 ? static_cast<int>(n) : 1;
#endif
    }();
    return num_cores;
}
