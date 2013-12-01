using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace VSWindowTitleChanger
{
	class Util
	{
		public static T Clone<T>(T source)
		{
			if (source == null)
				return default(T);
			if (!typeof(T).IsSerializable)
				throw new ArgumentException("Expected a serializable type!", "source");

			using (MemoryStream stream = new MemoryStream())
			{
				XmlSerializer xs = new XmlSerializer(typeof(T));
				xs.Serialize(stream, source);
				stream.Position = 0;
				return (T)xs.Deserialize(stream);
			}
		}


		public static string NormalizePath(string path, char path_separator)
		{
			// Changing path separators and eliminating duplicate path separator chars.
			StringBuilder sb = new StringBuilder();
			sb.Capacity = path.Length;
			bool prev_was_slash = false;
			foreach (char c in path)
			{
				switch (c)
				{
					case '\\':
					case '/':
						if (prev_was_slash)
							continue;
						prev_was_slash = true;
						sb.Append(path_separator);
						break;
					default:
						prev_was_slash = false;
						sb.Append(c);
						break;
				}
			}
			return sb.ToString();
		}

		public struct FilenameParts
		{
			public string path;				// the full pathname of the file
			public string dir;				// directory, there is no trailing path separator char
			public string file;				// filename without directory but with the extension included
			public string filename;			// filename without directory and extension
			public string ext;				// extension, e.g.: "txt"
		}

		public static void ProcessFilePath(string path, bool use_forward_slashes, ref FilenameParts parts)
		{
			if (path == null)
				path = "";
			char sep = use_forward_slashes ? '/' : '\\';
			path = NormalizePath(path, sep);
			parts.path = path;

			int idx = path.LastIndexOf(sep);
			if (idx < 0)
			{
				parts.dir = "";
				parts.file = path;
			}
			else
			{
				parts.dir = path.Substring(0, idx);
				parts.file = path.Substring(idx + 1);
			}

			idx = parts.file.LastIndexOf('.');
			if (idx < 0)
			{
				parts.filename = parts.file;
				parts.ext = "";
			}
			else
			{
				parts.filename = parts.file.Substring(0, idx);
				parts.ext = parts.file.Substring(idx + 1);
			}
		}
	}

}
