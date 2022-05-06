using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// https://www.loc.gov/preservation/digital/formats/fdd/fdd000504.shtml
/// http://formats.kaitai.io/stl/index.html
/// </summary>
public class StlFile
{
    const int HEADER_SIZE = 84;
    const int JUNK_SIZE = 80;
    const int SIZE_OF_FACET = 50;

    public struct Vertex
    {
        public float X, Y, Z;

        public override string ToString() => $"X:{X}, Y:{Y}, Z:{Z}";

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    }

    public struct Facet
    {
        public Vertex normal;
        public Vertex v1, v2, v3;
    };

    private readonly FileStream fileStream;
    private string FileName { get; }

    public Vertex[] Vertices { get; set; }

    public Facet[] Facets { get; set; }

    public StlFile(FileStream fileStream, string fileName)
    {
        this.fileStream = fileStream;
        this.FileName = fileName;

        bool isLoaded = ReadBinary(fileStream);
        if (!isLoaded)
        {
            fileStream.Position = 0;
            isLoaded = ReadAscii(fileStream);
        }

        if (isLoaded)
        {
            ComputeStats();
        }
    }

    public Vertex translate;
    public Vertex msize, mmin, mmax;
    public float scale;

    private void ComputeStats()
    {
        Vertex min, max;
        min.X = Facets[0].v1.X;
        min.Y = Facets[0].v1.Y;
        min.Z = Facets[0].v1.Z;
        max.X = Facets[0].v1.X;
        max.Y = Facets[0].v1.Y;
        max.Z = Facets[0].v1.Z;

        var c =Facets.Length;
        for (int i = 0; i < c; i++)
        {
            var facet = Facets[i];
            min.X = Math.Min(min.X, facet.v1.X);
            min.X = Math.Min(min.X, facet.v2.X);
            min.X = Math.Min(min.X, facet.v3.X);
            min.Y = Math.Min(min.Y, facet.v1.Y);
            min.Y = Math.Min(min.Y, facet.v2.Y);
            min.Y = Math.Min(min.Y, facet.v3.Y);
            min.Z = Math.Min(min.Z, facet.v1.Z);
            min.Z = Math.Min(min.Z, facet.v2.Z);
            min.Z = Math.Min(min.Z, facet.v3.Z);
            max.X = Math.Max(max.X, facet.v1.X);
            max.X = Math.Max(max.X, facet.v2.X);
            max.X = Math.Max(max.X, facet.v3.X);
            max.Y = Math.Max(max.Y, facet.v1.Y);
            max.Y = Math.Max(max.Y, facet.v2.Y);
            max.Y = Math.Max(max.Y, facet.v3.Y);
            max.Z = Math.Max(max.Z, facet.v1.Z);
            max.Z = Math.Max(max.Z, facet.v2.Z);
            max.Z = Math.Max(max.Z, facet.v3.Z);
        }

        mmin = min;
        mmax = max;
        msize.X = max.X - min.X;
        msize.Y = max.Y - min.Y;
        msize.Z = max.Z - min.Z;

        float globalMax = 0;
        globalMax = Math.Max(globalMax, max.X - min.X);
        globalMax = Math.Max(globalMax, max.Y - min.Y);
        globalMax = Math.Max(globalMax, max.Z - min.Z);
        scale = 1 / globalMax;

        translate.X = (float)(-(((max.X - min.X) / 2) + min.X));
        translate.Y = (float)(-(((max.Y - min.Y) / 2) + min.Y));
        translate.Z = (float)(-(((max.Z - min.Z) / 2) + min.Z));
    }

    private bool ReadBinary(FileStream fileStream)
    {
        var reader = new BinaryReader(fileStream);

        var fileContentSize = reader.BaseStream.Length - HEADER_SIZE;
        var fileSize = reader.BaseStream.Length;

        if (fileContentSize % SIZE_OF_FACET != 0)
        {
            return false;
        }

        for (var i = 0; i < JUNK_SIZE; i++)
        {
            fileStream.ReadByte();
        }

        var numFacets = fileContentSize / SIZE_OF_FACET;
        var headerNumFacets = reader.ReadUInt32();
        if (numFacets != headerNumFacets)
        {
            return false;
        }

        var facets = new List<Facet>();
        var vertices = new List<Vertex>();

        while (numFacets-- > 0)
        {
            Facet facet = default;

            facet.normal = ReadBinaryVertex(reader);
            facet.v1 = ReadBinaryVertex(reader);
            facet.v2 = ReadBinaryVertex(reader);
            facet.v3 = ReadBinaryVertex(reader);

            vertices.Add(facet.v1);
            vertices.Add(facet.v2);
            vertices.Add(facet.v3);

            reader.ReadUInt16();

            facets.Add(facet);
        }

        this.Facets = facets.ToArray();
        this.Vertices = vertices.ToArray();

        return true;
    }

    private Vertex ReadBinaryVertex(BinaryReader reader)
    {
        Vertex vertex = default;
        vertex.X = ReadBinaryFloat(reader);
        vertex.Z = ReadBinaryFloat(reader);
        vertex.Y = ReadBinaryFloat(reader);
        return vertex;
    }

    private float ReadBinaryFloat(BinaryReader reader)
    {
        StlFloat value = default;
        value.intValue = reader.ReadByte();
        value.intValue |= reader.ReadByte() << 0x08;
        value.intValue |= reader.ReadByte() << 0x10;
        value.intValue |= reader.ReadByte() << 0x18;
        return value.floatValue;
    }

    private bool ReadAscii(FileStream fileStream)
    {
        var sr = new StreamReader(fileStream);
        sr.ReadLine(); // solid 

        var facets = new List<Facet>();
        var vertices = new List<Vertex>();

        while (!sr.EndOfStream)
        {
            Facet facet = default;

            var line = sr.ReadLine(); // facet normal

            if (line.StartsWith("endsolid"))
                break;

            facet.normal = ReadAsciiVertex(line, 2);

            line = sr.ReadLine(); // outer loop
            line = sr.ReadLine(); // vertex
            facet.v1 = ReadAsciiVertex(line);

            line = sr.ReadLine(); // vertex
            facet.v2 = ReadAsciiVertex(line);

            line = sr.ReadLine(); // vertex
            facet.v3 = ReadAsciiVertex(line);

            line = sr.ReadLine(); // endloop
            line = sr.ReadLine(); // endfacet

            vertices.Add(facet.v1);
            vertices.Add(facet.v2);
            vertices.Add(facet.v3);
            facets.Add(facet);
        }

        this.Facets = facets.ToArray();
        this.Vertices = vertices.ToArray();

        return true;

    }

    private string ReadAsciiWord(StreamReader sr)
    {
        while (sr.Peek() >= 0)
        {
            var c = (char)sr.Peek();
            if (!isWhiteSpace(c))
                break;
            sr.Read();
        }

        var word = string.Empty;

        while (!sr.EndOfStream)
        {
            var c = (char)sr.Read();
            if (isWhiteSpace(c))
                break;
            word += c;
        }

        return word;

        bool isWhiteSpace(char c) => c.Equals(' ') || c.Equals('\t') || c.Equals('\n') || c.Equals('\r');
    }

    private Vertex ReadAsciiVertex(string line, int skip = 1)
    {
        using var lineReader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(line)));

        while (skip-- > 0)
        {
            ReadAsciiWord(lineReader);
        }

        return new Vertex()
        {
            X = Convert.ToSingle(ReadAsciiWord(lineReader)),
            Z = Convert.ToSingle(ReadAsciiWord(lineReader)),
            Y = Convert.ToSingle(ReadAsciiWord(lineReader)),
        };
    }

    public void WriteBinary(FileStream outputFileStream)
    {
        var binaryWriter = new BinaryWriter(outputFileStream);

        var headerBytes = Encoding.UTF8.GetBytes("Appbyfex.DotnetSTLFiles");
        Array.Resize(ref headerBytes, JUNK_SIZE);
        binaryWriter.Write(headerBytes);

        binaryWriter.Write((uint)this.Facets.Length);

        var facetCount = Facets.Length;
        foreach (var facet in Facets)
        {
            WriteFacetBinary(binaryWriter, facet);

            binaryWriter.Write((ushort)0);
        }
    }

    private void WriteFacetBinary(BinaryWriter binaryWriter, Facet facet)
    {
        WriteVertexBinary(binaryWriter, facet.normal);
        WriteVertexBinary(binaryWriter, facet.v1);
        WriteVertexBinary(binaryWriter, facet.v2);
        WriteVertexBinary(binaryWriter, facet.v3);
    }

    private static void WriteVertexBinary(BinaryWriter binaryWriter, Vertex vertex)
    {
        binaryWriter.Write(vertex.X);
        binaryWriter.Write(vertex.Y);
        binaryWriter.Write(vertex.Z);
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct StlFloat
    {
        [FieldOffset(0)] public int intValue;
        [FieldOffset(0)] public float floatValue;
    }
}
