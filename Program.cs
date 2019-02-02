using System;
using System.Linq;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using TagLib;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace Bandcamp
{
    //To do: change to a better image (a2000554660_16.jpg => a2000554660_1.jpg/a2000554660_10.jpg) and download both
    class Program
    {
        const string rootPath = @"D:\Music\Bandcamp\";
        static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            string url = Console.ReadLine();
            string document = new StreamReader(WebRequest.Create(url).GetResponse().GetResponseStream()).ReadToEnd();
            Match title = new Regex(@"<title>([^\|<>]*)\|([^\|<>]*)</title>").Match(document);
            //string metaDescription = WebUtility.HtmlDecode(new Regex("< *meta *name *= *\"Description\" *content *= *\"([^\">]*)\">").Match(document).Groups[1].Value.Trim());
            var jsonMatch = new Regex(@"trackinfo:[ \r\n]*(\[(.(?!</script>))*)").Match(document).Groups[1].Value;
            //var jsonMatch = new Regex(@"trackinfo:[ \r\n]*(\[[^\[\]]*\])").Match(document).Groups[1].Value;
            JArray json = JArray.Parse(jsonMatch.Substring(0, jsonMatch.Length - 1));
            var artUrl = new Regex("image_src\" *href *= *\"([^\"]*)\"").Match(document).Groups[1].Value;

            using (HttpClient client = new HttpClient())
            {
                Album album = new Album();
                album.name = title.Groups[1].Value.Trim();
                album.band = title.Groups[2].Value.Trim();
                album.songs = new List<Song>();
                foreach (JToken song in json)
                    album.songs.Add(new Song { name = song["title"]?.Value<string>(), number = song.Contains("track_num") ? song["track_num"]?.Value<int>() ?? 0 : 0, url = song["file"]["mp3-128"]?.Value<string>() });

                var fullPath = rootPath + CleanForPath(album.band) + "\\";
                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);
                fullPath += CleanForPath(album.name) + "\\";
                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);

                album.artPath = fullPath + "art." + artUrl.Split('.').Last();
                var artBytes = client.GetByteArrayAsync(artUrl).Result;
                System.IO.File.WriteAllBytes(album.artPath, artBytes);

                foreach (var song in album.songs)
                {
                    var filePath = fullPath + string.Join("", CleanForPath(song.name)) + ".mp3";
                    var bytes = client.GetByteArrayAsync(song.url).Result;
                    System.IO.File.WriteAllBytes(filePath, bytes);

                    var tagFile = TagLib.File.Create(filePath);
                    if (string.IsNullOrEmpty(tagFile.Tag.Album))
                        tagFile.Tag.Album = album.name;
                    if (tagFile.Tag.AlbumArtists.Length == 0)
                        tagFile.Tag.AlbumArtists = new string[] { album.band };
                    if (string.IsNullOrEmpty(tagFile.Tag.Title))
                        tagFile.Tag.Title = song.name;
                    tagFile.Tag.Track = (uint)song.number;
                    tagFile.Tag.Pictures = new IPicture[] { new Picture(album.artPath) };
                    tagFile.Save();
                }
            }
            Console.WriteLine("Done");
            Console.ReadKey();
        }

        static string CleanForPath(string str)
        {
            var restrictions = new List<char> { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
            return string.Join("", str.Select(x => restrictions.Any(y => y == x) ? ' ' : x));
        }

        static string Capitalize(string s)
        {
            return string.Join(" ", s.Split(' ', '-', '/', '\\').Select(x => char.ToUpper(x[0]) + x.Substring(1)));
        }
        struct Album
        {
            public string name, band;
            public string artPath;
            public List<Song> songs;
        }
        struct Song
        {
            public string url, name;
            public int number;
        }
    }
}
