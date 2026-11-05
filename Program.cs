using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.SkiaSharp; // 关键：引入 ZXing 的 SkiaSharp 绑定
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

    try
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0; // 重置流位置

        // 1. 将字节流转为 SkiaSharp 的 SKBitmap
        using var skBitmap = SKBitmap.Decode(ms);
        if (skBitmap == null)
            return Results.Ok(new { success = false, text = (string?)null, msg = "图片解析失败" });

        // 2. 使用 ZXing 的 SKBitmapLuminanceSource 进行识别
        var source = new SKBitmapLuminanceSource(skBitmap);
        var reader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { TryHarder = true }
        };

        var result = reader.Decode(source);

        return result is null
            ? Results.Ok(new { success = false, text = (string?)null, msg = "未识别到二维码" })
            : Results.Ok(new { success = true, text = result.Text });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { success = false, msg = $"识别出错: {ex.Message}" });
    }
}).DisableAntiforgery();
#endregion

#region 上传文本，生成二维码接口
// ==========================================
app.MapPost("/api/qr/generate", (QrGenerateRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
        return Results.BadRequest(new { success = false, msg = "文本内容不能为空" });

    try
    {
        int width = request.Width > 0 ? request.Width : 300;
        int height = request.Height > 0 ? request.Height : 300;

        // 1. 使用 SkiaSharp 的 BarcodeWriter
        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 1
            }
        };

        // 2. 直接生成 SKBitmap
        using var skBitmap = writer.Write(request.Text);

        // 3. 编码为 PNG 格式的字节流
        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        byte[] imageBytes = data.ToArray();

        // 4. 返回图片流
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
