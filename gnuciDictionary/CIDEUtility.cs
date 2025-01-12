﻿using Common;
using Common.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace gnuciDictionary
{
	internal static class CIDEUtility
	{
		public static void LoadFromCIDEFiles(string input, string output)
		{
			var data = new ConcurrentDictionary<string, Dictionary<string, List<Word>>>();
			void AddWord(string v)
			{
				var lines = v.Split('\n');
				var val = Regex.Match(lines[0], "<p><ent>(.*)</ent>").Groups[1].Value.Trim();
				var slug = val.ToLowerInvariant();
				if (string.IsNullOrEmpty(val))
				{
					return;
				}
				var def = Regex.Match(v, "<def>(.*)</def>").Groups[1]?.Value;
				def = StringExtensions.ReplaceAll(def, "<.+?>", "");
				var plural = Regex.Match(v, "<plw>(.*)</plw>").Groups[1]?.Value;
				var wordType = Regex.Match(v, "<pos>(.*)</pos>").Groups[1]?.Value;
				var peek = gnuciDictionary.EnglishDictionary.GetPeekValue(val);
				if (!data.TryGetValue(peek, out var bucket))
				{
					bucket = new Dictionary<string, List<Word>>();
					data.TryAdd(peek, bucket);
				}
				if (!bucket.TryGetValue(slug, out var list))
				{
					list = new List<Word>();
					bucket[slug] = list;
				}

				var word = new Word(val, def, plural, wordType);
				Logger.Debug($"{peek}: {word}");
				list.Add(word);
			}
			void ReadFile(string file)
			{
				Logger.Debug($"Loading {file}");
				var lines = File.ReadAllLines(file);
				StringBuilder sb = null;
				for (int i = 0; i < lines.Length; i++)
				{
					var line = lines[i];
					if (line.StartsWith("<p><ent>"))
					{
						sb = new StringBuilder(line + "\n");
					}
					if (sb != null)
					{
						if (string.IsNullOrEmpty(line.Trim()))
						{
							AddWord(sb.ToString());
							sb = null;
						}
						else
						{
							sb.AppendLine(line);
						}
					}
				}
			}

			var files = Directory.GetFiles(input, "CIDE.*");
			var tasks = new Task[files.Length];
			for (int i = 0; i < files.Length; i++)
			{
				var filePath = files[i];
				tasks[i] = new Task(() => ReadFile(filePath));
				tasks[i].Start();
			}
			Task.WaitAll(tasks);
			var dict = data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

			if (Directory.Exists(output))
			{
				Directory.Delete(output, true);
			}
			Directory.CreateDirectory(output);
			foreach (var kvp in data)
			{
				var path = Path.Combine(output, $"dict_{kvp.Key}.dat");
				var str = JsonConvert.SerializeObject(kvp.Value);
				try
				{
					UnicodeEncoding uniEncode = new UnicodeEncoding();
					byte[] bytesToCompress = uniEncode.GetBytes(str);
					var xstr = uniEncode.GetString(bytesToCompress);
					if (str != xstr)
					{
						throw new Exception();
					}
					using (FileStream fileToCompress = File.Create(path))
					{
						using (GZipStream compressionStream = new GZipStream(fileToCompress, CompressionMode.Compress))
						{
							compressionStream.Write(bytesToCompress, 0, bytesToCompress.Length);
						}
					}
				}
				catch (Exception e)
				{
					Logger.Exception(e, $"Failed to serialize to {path}");
				}
			}
		}

	}
}
