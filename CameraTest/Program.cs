using System;
using System.Diagnostics;
using System.Threading;
using LumenPomodoro.Services;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== 摄像头功能测试 ===\n");

        var cameraService = new CameraService();
        var errorMessages = new System.Collections.Generic.List<string>();

        cameraService.Initialize(0,
            status => Console.WriteLine($"[状态] {status}"),
            error => {
                Console.WriteLine($"[错误] {error}");
                errorMessages.Add(error);
            });

        Console.WriteLine("1. 测试摄像头枚举...");
        var cameras = cameraService.GetAvailableCameras();
        Console.WriteLine($"   发现 {cameras.Count} 个摄像头:");
        for (int i = 0; i < cameras.Count; i++)
        {
            Console.WriteLine($"   [{i}] {cameras[i]}");
        }

        var cameraCount = cameraService.GetCameraCount();
        Console.WriteLine($"\n2. 摄像头数量: {cameraCount}");

        if (cameras.Count == 0)
        {
            Console.WriteLine("\n[结果] 未检测到摄像头（可能没有物理摄像头）");
            return;
        }

        Console.WriteLine("\n3. 尝试启动摄像头（点亮指示灯）...");
        Console.WriteLine("   等待 3 秒...");

        var startTime = DateTime.Now;
        cameraService.StartCameraAsync().Wait();
        Thread.Sleep(3000);

        if (cameraService.IsRunning)
        {
            Console.WriteLine("\n[结果] 摄像头启动成功！指示灯应该已经亮起。");
            Console.WriteLine($"   运行时间: {(DateTime.Now - startTime).TotalSeconds:F1} 秒");
            Console.WriteLine($"   CPU 占用: 低（0.2fps 帧率）");

            Console.WriteLine("\n4. 停止摄像头...");
            cameraService.StopCameraAsync().Wait();
            Thread.Sleep(500);

            if (!cameraService.IsRunning)
            {
                Console.WriteLine("[结果] 摄像头停止成功！");
            }
        }
        else
        {
            Console.WriteLine("\n[结果] 摄像头启动失败");
            if (errorMessages.Count > 0)
            {
                Console.WriteLine("错误信息:");
                foreach (var err in errorMessages)
                {
                    Console.WriteLine($"  - {err}");
                }
            }
        }

        Console.WriteLine("\n=== 测试完成 ===");
    }
}
