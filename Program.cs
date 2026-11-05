using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using System.Drawing; 
using System.IO;
var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();






#region// 接口：上传图片二进制，返回识别结果
app.MapPost("/api/qr/decode", async (HttpRequest req) =>
{
    if (!req.HasFormContentType)
        return Results.BadRequest(new { success = false, msg = "请用 multipart/form-data 上传图片" });

    var form = await req.ReadFormAsync();
    var file = form.Files.GetFile("image");
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { success = false, msg = "缺少 image 文件" });

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    var text = DecodeQr(ms.ToArray());

    return text is null
        ? Results.Ok(new { success = false, text = (string?)null, msg = "识别失败" })
        : Results.Ok(new { success = true, text });
})
.DisableAntiforgery();
#endregion

#region 上传文本，生成二维码接口
// ==========================================
app.MapPost("/api/qr/generate", (QrGenerateRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { success = false, msg = "文本内容不能为空" });
    }

    try
{
    // 1. 设置二维码生成参数 (可调整大小)
    int width = request.Width > 0 ? request.Width : 300;
    int height = request.Height > 0 ? request.Height : 300;

    var writer = new BarcodeWriterPixelData
    {
        Format = BarcodeFormat.QR_CODE,
        Options = new QrCodeEncodingOptions
        {
            Width = width,
            Height = height,
            Margin = 1 // 边框留白
        }
    };
        Console.WriteLine($"生成二维码: {request.Text}, 尺寸: {width}x{height}");
        // 2. 生成二维码像素数据
        var pixelData = writer.Write(request.Text);

    // 3. 将像素数据转换为 Bitmap，再保存为 PNG 的内存流
    using var bitmap = new Bitmap(pixelData.Width, pixelData.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
    var bitmapData = bitmap.LockBits(
        new Rectangle(0, 0, pixelData.Width, pixelData.Height),
        System.Drawing.Imaging.ImageLockMode.WriteOnly,
        System.Drawing.Imaging.PixelFormat.Format32bppRgb);

    try
    {
        System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
    }
    finally
    {
        bitmap.UnlockBits(bitmapData);
    }

    // 4. 保存为 PNG 并转成 Base64
    using var ms = new MemoryStream();
    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
    byte[] imageBytes = ms.ToArray();
    string base64String = Convert.ToBase64String(imageBytes);

        // 5. 直接返回文件流 (浏览器和 Postman 都可以直接预览图片)
        return Results.File(imageBytes, "image/png");
    }
catch (Exception ex)
{
    return Results.Problem($"生成二维码失败: {ex.Message}");
}
});

#endregion





app.Run();



#region// 核心识别函数
static string? DecodeQr(byte[] imageBytes)
{
    try
    {
        using var ms = new MemoryStream(imageBytes);
        using var bmp = new Bitmap(ms);

        // 转 RGB24
        byte[] rgbData = new byte[bmp.Width * bmp.Height * 3];
        int index = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var p = bmp.GetPixel(x, y);
                rgbData[index++] = p.R;
                rgbData[index++] = p.G;
                rgbData[index++] = p.B;
            }

        var source = new RGBLuminanceSource(
            rgbData, bmp.Width, bmp.Height,
            RGBLuminanceSource.BitmapFormat.RGB24);

        var reader = new BarcodeReaderGeneric()
        {
            Options = new DecodingOptions { TryHarder = true }
        };

        return reader.Decode(source)?.Text;
    }
    catch { return null; }
}
#endregion

#region 定义接收文本的请求体模型
public class QrGenerateRequest
{
    public string Text { get; set; } = string.Empty;
    public int Width { get; set; } = 300;  // 可选：默认宽 300
    public int Height { get; set; } = 300; // 可选：默认高 300
}
#endregion
