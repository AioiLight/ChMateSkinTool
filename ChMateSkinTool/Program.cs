using System;
using System.Linq;
using SharpAdbClient;
using System.IO;
using System.Net;
using System.Threading;

namespace ChMateSkinTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ChMateSkinTool Rev.1");
            Console.WriteLine("あらかじめ端末とPCをUSB接続してください。");

            Console.WriteLine("PC側のChMateテーマのあるディレクトリを入力してください。\nそのディレクトリを監視して変更があれば転送します。");
            Console.WriteLine(@"e.g. C:\Dev\ChMate\MyTheme");
            Console.Write("ディレクトリ:");
            SkinDir = Console.ReadLine();

            Console.WriteLine();

            Console.WriteLine("端末側のChMateテーマを配置するディレクトリを入力してください。\nそのフォルダを監視して変更があれば転送します。");
            Console.WriteLine(@"e.g. /storage/emulated/0/2chMate/theme/MyTheme");
            Console.Write("ディレクトリ:");
            ThemeDir = Console.ReadLine();

            // 最後にスラッシュがなかったら付け足す
            if (!ThemeDir.EndsWith("/"))
            {
                ThemeDir += '/';
            }

            Console.WriteLine();

            Console.WriteLine("platform-toolsのadb.exeのパスを入力してください。");
            Console.Write("パス:");
            var adbExe = Console.ReadLine();

            var server = new AdbServer();
            var result = server.StartServer(adbExe, false);


            Console.WriteLine();
            Console.WriteLine("接続されているデバイスを検索します...USBデバッグの許可を求められたら、許可を押してください。");
            Console.WriteLine();
            var index = 0;
            foreach (var item in AdbClient.Instance.GetDevices())
            {
                Console.WriteLine("インデックス:{0} / ID:{1} / 端末名:{2} / モデル名:{3}", index, item.TransportId, item.Name, item.Model);
                index++;
            }
            Console.WriteLine();
            Console.WriteLine("どれに接続しますか？インデックスを入力してください。(省略でインデックス0の端末)");
            Console.WriteLine();
            var inputID = Console.ReadLine();

            if (inputID.Length > 0 && int.TryParse(inputID, out var id) && id < AdbClient.Instance.GetDevices().Count)
            {
                Device = AdbClient.Instance.GetDevices()[id];
            }
            else
            {
                if (inputID.Length != 0)
                {
                    Device = AdbClient.Instance.GetDevices().First();
                }
                else
                {
                    Console.WriteLine("ひとつも端末が接続されていないか、なんらかのエラーです。\n" +
                        "USBデバッグを許可して、もう一度やり直してみてください。\n" +
                        "またUSBケーブルは充電用では通信できません。");
                    Exit();
                    return;
                }
            }

            Console.WriteLine("接続しました。");
            var watcher = new FileSystemWatcher();
            try
            {
                watcher.Path = SkinDir;
            }
            catch (ArgumentException)
            {
                Console.WriteLine("監視先のディレクトリがおかしいです。\n" +
                    "存在し、かつ権限のあるディレクトリを指定してください。");
                Exit();
                return;
            }
            watcher.Filter = "";
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.Changed += CopyTheme;
            watcher.Deleted += CopyTheme;
            watcher.Created += CopyTheme;

            try
            {
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception)
            {
                Console.WriteLine("監視の開始に失敗しました。なんかおかしいです……。");
                Exit();
                return;
            }

            Console.WriteLine("{0}ディレクトリの監視を開始しました。Enterを押すと終了します。", SkinDir);

            Exit();

        }

        private static void CopyTheme(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("更新を検知しました:");
            AdbClient.Instance.ExecuteRemoteCommand(string.Format("mkdir {0}", ThemeDir), Device, null);

            // *.*で全部
            var files = new DirectoryInfo(SkinDir).GetFiles("*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                try
                {
                    using (var service = new SyncService(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)), Device))
                    {
                        var path = file.FullName.Substring(SkinDir.Length + 1);
                        var to = path.Replace('\\', '/');
                        using (Stream stream = File.OpenRead(file.FullName))
                        {
                            service.Push(stream, ThemeDir + to, 444, DateTime.Now, null, CancellationToken.None); ;

                            Console.WriteLine("コピーしました:{0} → {1}", path, ThemeDir + to);
                        }
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("ファイルのコピーに失敗しました。なにかがおかしいです……。\n" +
                        "接続先と接続されているか、コピー先の権限は足りているかどうか確認してください。");
                    Exit();
                    return;
                }
            }

            // ChMateのタスクキルと起動
            KillAndStartChMate();
        }

        private static void KillAndStartChMate()
        {
            Console.WriteLine("ChMateの終了:");
            AdbClient.Instance.ExecuteRemoteCommand("am force-stop jp.co.airfront.android.a2chMate", Device, null);
            Console.WriteLine("ChMateの開始:");
            AdbClient.Instance.ExecuteRemoteCommand("am start -n jp.co.airfront.android.a2chMate/jp.syoboi.a2chMate.activity.HomeActivity", Device, null);
        }

        private static void Exit()
        {
            Console.ReadLine();
        }

        public static DeviceData Device { get; set; }
        public static string SkinDir { get; set; }
        public static string ThemeDir { get; set; }
    }
}
