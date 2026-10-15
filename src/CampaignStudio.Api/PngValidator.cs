using System.Buffers.Binary;
using System.Text;
namespace CampaignStudio.Api;
public static class PngValidator
{
    private static readonly uint[] Table=Enumerable.Range(0,256).Select(n=>{uint c=(uint)n;for(int i=0;i<8;i++)c=(c&1)!=0?0xedb88320^(c>>1):c>>1;return c;}).ToArray();
    private static uint Crc(ReadOnlySpan<byte> bytes){uint c=0xffffffff;foreach(byte b in bytes)c=Table[(int)((c^b)&255)]^(c>>8);return c^0xffffffff;}
    public static void Check(byte[] bytes)
    {
        if(bytes.Length<33||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new FormatException("Invalid PNG header.");
        uint w=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)),h=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
        if(w==0||h==0||w>10000||h>10000||(long)w*h>25_000_000)throw new FormatException("PNG dimensions exceed 25 megapixels.");
        int offset=8;bool data=false,end=false;
        while(offset<bytes.Length)
        {
            if(offset+12>bytes.Length)throw new FormatException("Truncated PNG.");
            uint length=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset,4));if(length>5_000_000||(long)offset+12+length>bytes.Length)throw new FormatException("Invalid PNG chunk.");
            int size=(int)length;string kind=Encoding.ASCII.GetString(bytes,offset+4,4);
            if(offset==8&&(kind!="IHDR"||size!=13))throw new FormatException("Invalid PNG header.");
            if(Crc(bytes.AsSpan(offset+4,size+4))!=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset+8+size,4)))throw new FormatException("PNG checksum failed. Export the image again.");
            if(kind=="IDAT")data=true;offset+=size+12;if(kind=="IEND"){end=size==0&&offset==bytes.Length;break;}
        }
        if(!data||!end)throw new FormatException("Incomplete PNG.");
    }
}
