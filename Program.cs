// See https://aka.ms/new-console-template for more information

using System.Drawing;
using System.Text.RegularExpressions;

class BCWThumbnailGen
{
    //Config, set by user
    static string rootDir = Environment.CurrentDirectory;
    static bool showFetchedItems = false;
    static List<string> ignoreDirConfig = [".git", "profile", "gameitem"];
    static string sourceImgDirNameConfig = "imgsource";
    static int imgTargetWidthConfig = 700;
    static List<string> TargetType = new List<string> { "fastimg" };

    //Processed Config, used by the program
    static List<string> ignoreDir = [];

    static void Main(string[] args)
    {
        Console.WriteLine("BCW Thumbnail Generator Running.");
        Console.WriteLine("RootDir:" + rootDir);

        ignoreDir.Clear();
        foreach (string dir in ignoreDirConfig)
        {
            DirectoryInfo igdir = new(rootDir + "\\" + dir);
            Console.WriteLine("Ignored: " + igdir.FullName);
            ignoreDir.Add(igdir.FullName);
        }
        ProcessAllDir();
        Console.WriteLine("Thumbnail generation finished.");
    }

    public static void ProcessAllDir()
    {
        ProcessFiles(new DirectoryInfo(rootDir));
        GetDirectories(new DirectoryInfo(rootDir));
    }

    public static void GetDirectories(DirectoryInfo dirRoot)
    {
        DirectoryInfo[] dirList = dirRoot.GetDirectories();
        foreach (DirectoryInfo dirinfo in dirList)
        {
            if (ignoreDir.Contains(dirinfo.FullName))
            {
                continue;
            }

            if (showFetchedItems)
                Console.WriteLine("Fetched Dir: " + GetRelativePath(dirinfo.FullName));
            if (Directory.Exists(dirinfo.FullName))
            {
                GetDirectories(new DirectoryInfo(dirinfo.FullName + "\\"));
                ProcessFiles(dirinfo);
            }
        }
    }

    static void ProcessFiles(DirectoryInfo dirinfo)
    {
        FileInfo[] files = dirinfo.GetFiles();
        foreach (FileInfo file in files)
        {
            if (showFetchedItems == true)
                Console.WriteLine("Fetched file: " + GetRelativePath(file.FullName));

            if (file.Extension == ".html")
            {
                ProcessHTMLSource(file);
            }
        }
    }
    static bool IsValidImage(string path)
    {
        try
        {
            // 读取文件头验证
            byte[] header = new byte[8];
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                fs.Read(header, 0, header.Length);
            }

            // PNG文件头：89 50 4E 47 0D 0A 1A 0A
            if (header[0] == 0x89 && header[1] == 0x50 &&
                header[2] == 0x4E && header[3] == 0x47)
                return true;

            // 添加其他格式检测...
            return false;
        }
        catch
        {
            return false;
        }
    }

    static void ProcessHTMLSource(FileInfo file)
    {
        List<string> imgSources = [];
        string htmlContent = System.IO.File.ReadAllText(file.FullName);
        string baseDir = Path.GetDirectoryName(file.FullName);
        var targetImages = new List<string>();
        bool HTMLUpdated = false;
        // 构建组合正则表达式模式
        string classPattern = "(?:" + string.Join("|", TargetType) + ")";
        string imgRegexPattern = $@"
            <img
                \s+ 
                (?:[^>]*?\s+)?
                class\s*=\s*['""]
                [^'""]*?\b({classPattern})\b[^'""]*  # 匹配目标class
                ['""]
                [^>]*?
                src\s*=\s*['""](?<src>[^'""]+)['""]
                [^>]*>
            ";

        var matches = Regex.Matches(
            htmlContent,
            imgRegexPattern,
            RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline
        );

        foreach (Match match in matches)
        {
            string src = match.Groups["src"].Value.Trim();

            string imgPath = Path.Combine(baseDir, src.Split(new[] { '?', '#' })[0]); // 清理URL参数和锚点

            if (!File.Exists(imgPath))
            {
                Console.WriteLine($"Image not existing: {GetRelativePath(imgPath)}");
                continue;
            }

            try
            {
                bool needCreateThumbnail = false;
                using (var stream = File.OpenRead(imgPath))
                {
                    if (!IsValidImage(imgPath))
                    {
                        Console.WriteLine($"Invalid image: {GetRelativePath(imgPath)}");
                        continue;
                    }
                    using (var image = Image.FromStream(stream, false, false))
                    {
                        if (image.Width > imgTargetWidthConfig)
                        {
                            needCreateThumbnail = true;
                        }
                        image.Dispose();
                    }
                    stream.Close();
                }
                if (needCreateThumbnail)
                {
                    string newSrc = Regex.Replace(src,
                         @"\.png(?=\?|#|$)",
                         ".webp",
                         RegexOptions.IgnoreCase);
                    ProcessImg(new FileInfo(imgPath));
                    htmlContent = htmlContent.Replace(
                        match.Value,
                        match.Value.Replace(src, newSrc));
                    HTMLUpdated = true;
                }
            }
            catch (OutOfMemoryException) // 专门捕获图像解析错误
            {
                Console.WriteLine($"Invalid image file: {GetRelativePath(imgPath)}");
                continue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process [{GetRelativePath(imgPath)}]: {ex.Message}");
                continue;
            }

            targetImages.Add(src);
        }

        if (HTMLUpdated)
        {
            File.WriteAllText(file.FullName, htmlContent);
            Console.WriteLine($"Updated File: {GetRelativePath(file.FullName)}");
        }

    }

    static void ProcessImg(FileInfo file)
    {
        Console.WriteLine("Processing img");
        DirectoryInfo dirInfo = new(file.DirectoryName);
        bool haveImageSourceFolder = false;
        DirectoryInfo[] subFolders = dirInfo.GetDirectories();
        foreach (DirectoryInfo subFolder in subFolders)
        {
            if (subFolder.Name == sourceImgDirNameConfig)
            {
                haveImageSourceFolder = true;
                break;
            }
        }
        if (showFetchedItems)
            Console.WriteLine("Fetched Img: " + GetRelativePath(file.FullName));

        int imgWidth = -1;
        try
        {
            using (Stream stream = File.OpenRead(file.FullName))
            {
                using (Image sourceimage = Image.FromStream(stream, false, false))
                {
                    imgWidth = sourceimage.Width;
                    sourceimage.Dispose();
                }
                stream.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to load image: " + ex);
        }

        if (imgWidth > imgTargetWidthConfig)
        {
            Image img = Image.FromFile(file.FullName);
            int newWidth = imgTargetWidthConfig;
            float percent = (float)newWidth / (float)img.Width;
            int newHeight = (int)(img.Height * percent);
            Bitmap resizedImg = new(newWidth, newHeight);
            using (Graphics graphics = Graphics.FromImage(resizedImg))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(img, 0, 0, newWidth, newHeight);
                graphics.Dispose(); Console.WriteLine(newWidth + "  " + newHeight);
            }
            img.Dispose();
            try
            {
                if (!haveImageSourceFolder)
                {
                    Directory.CreateDirectory(dirInfo + "\\" + sourceImgDirNameConfig);
                }
                DirectoryInfo sourceImgFolder = new(dirInfo + "\\" + sourceImgDirNameConfig);

                bool sourceImgAlreadyExists = false;
                FileInfo[] sourceFiles = sourceImgFolder.GetFiles();
                foreach (FileInfo sourceFile in sourceFiles)
                {
                    if (sourceFile.Name == file.Name)
                    {
                        sourceImgAlreadyExists = true; ;
                        break;
                    }
                }

                string fileFullName = file.FullName;

                if (!sourceImgAlreadyExists)
                {
                    file.CopyTo(Path.Combine(sourceImgFolder.FullName , file.Name));
                    File.Delete(file.FullName);
                }
                else
                {
                    File.Delete(file.FullName);
                }

                resizedImg.Save(Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(fileFullName) + ".webp"), System.Drawing.Imaging.ImageFormat.Webp);
                resizedImg.Dispose();
                Console.WriteLine("Processed img: " + GetRelativePath(file.FullName));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to write image: " + ex);
            }
        }
    }

    public static string GetRelativePath(string pathFullName)
    {
        return "\\" + Path.GetRelativePath(rootDir, pathFullName);
    }
}
