using System.Collections.Generic;
using Utf8Json;

namespace UndeadLegacyPanels
{
	/// <summary>
	/// Small helpers on top of Utf8Json.JsonWriter, matching the manual streaming-write pattern
	/// used throughout the stock WebServer/MapRendering API classes (Map.cs, Mods.cs) - used
	/// instead of JsonSerializer.Serialize&lt;T&gt;'s reflection-based path, since none of the
	/// existing code in this codebase exercises that path and its default naming/field-vs-property
	/// behavior in this exact bundled Utf8Json build is unconfirmed.
	/// </summary>
	public static class JsonWriteHelpers
	{
		public static void WriteStringArray(ref JsonWriter writer, IEnumerable<string> values)
		{
			writer.WriteBeginArray();
			bool first = true;
			foreach (string value in values)
			{
				if (!first)
				{
					writer.WriteValueSeparator();
				}
				writer.WriteString(value);
				first = false;
			}
			writer.WriteEndArray();
		}
	}
}
