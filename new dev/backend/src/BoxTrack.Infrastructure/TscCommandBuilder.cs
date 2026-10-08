using System.Text;

namespace BoxTrack.Infrastructure;

public sealed class TscCommandBuilder
{
    private readonly MemoryStream _stream = new();
    private readonly StringBuilder _textLog = new();

    private void AppendAscii(string command)
    {
        var bytes = Encoding.ASCII.GetBytes(command + "\r\n");
        _stream.Write(bytes, 0, bytes.Length);
        _textLog.AppendLine(command);
    }

    public TscCommandBuilder SetSize(double widthMm, double heightMm)
    {
        AppendAscii($"SIZE {widthMm:0.##} mm, {heightMm:0.##} mm");
        return this;
    }

    public TscCommandBuilder SetGap(double gapMm, double offsetMm = 0)
    {
        AppendAscii($"GAP {gapMm:0.##} mm, {offsetMm:0.##} mm");
        return this;
    }

    public TscCommandBuilder SetDirection(int direction)
    {
        // 0 or 1
        AppendAscii($"DIRECTION {direction}");
        return this;
    }

    public TscCommandBuilder Clear()
    {
        AppendAscii("CLS");
        return this;
    }

    public TscCommandBuilder AddBox(int xDots, int yDots, int endXDots, int endYDots, int thicknessDots = 2)
    {
        AppendAscii($"BOX {xDots},{yDots},{endXDots},{endYDots},{thicknessDots}");
        return this;
    }

    public TscCommandBuilder AddText(
        int xDots,
        int yDots,
        string font,
        int rotation,
        int xMul,
        int yMul,
        string content)
    {
        // TSPL TEXT x,y,"font",rotation,x-multiplication,y-multiplication,"content"
        var escaped = content.Replace("\"", "\\[\"]");
        AppendAscii($"TEXT {xDots},{yDots},\"{font}\",{rotation},{xMul},{yMul},\"{escaped}\"");
        return this;
    }

    public TscCommandBuilder AddBarcode(
        int xDots,
        int yDots,
        string barcodeType,
        int heightDots,
        bool humanReadable,
        int rotation,
        int narrowDots,
        int wideDots,
        string content)
    {
        // TSPL BARCODE X,Y,"code type",height,human readable,rotation,narrow,wide,"content"
        var readable = humanReadable ? 1 : 0;
        var escaped = content.Replace("\"", string.Empty);
        AppendAscii($"BARCODE {xDots},{yDots},\"{barcodeType}\",{heightDots},{readable},{rotation},{narrowDots},{wideDots},\"{escaped}\"");
        return this;
    }

    public TscCommandBuilder AddBitmap(
        int xDots,
        int yDots,
        int widthBytes,
        int heightDots,
        int mode,
        byte[] bitmapData)
    {
        // TSPL command header: BITMAP X,Y,width_bytes,height,mode,
        var header = $"BITMAP {xDots},{yDots},{widthBytes},{heightDots},{mode},";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        _stream.Write(headerBytes, 0, headerBytes.Length);
        _stream.Write(bitmapData, 0, bitmapData.Length);
        _stream.Write(Encoding.ASCII.GetBytes("\r\n"), 0, 2);

        _textLog.AppendLine($"{header}<{bitmapData.Length} bytes bitmap data>");
        return this;
    }

    public TscCommandBuilder Print(int sets, int copies = 1)
    {
        AppendAscii($"PRINT {sets},{copies}");
        return this;
    }

    public byte[] BuildPayload() => _stream.ToArray();
    public string BuildCommandText() => _textLog.ToString();
}
