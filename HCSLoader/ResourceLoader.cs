using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace HCSLoader
{
	public static class ResourceLoader
	{
		public static Sprite LoadSprite(string path, Vector2? anchor = null)
		{
			var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
			texture.LoadImage(File.ReadAllBytes(path));

			return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), anchor ?? new Vector2(texture.width / (float)2, texture.height / (float)2));
		}

		public static AudioClip LoadClip(string path)
		{
			var www = new WWW("file:///" + path.Replace('\\', '/'));

			var clip = www.GetAudioClip(false, false, AudioType.OGGVORBIS);

			clip.LoadAudioData();

			while (!clip.isReadyToPlay)
				Thread.Sleep(5);

			return clip;
		}

		public static AudioGroup LoadAudioGroup(string directory)
		{
			return new AudioGroup
			{
				clips = Directory.GetFiles(directory, "*.ogg", SearchOption.AllDirectories)
								 .Select(x => LoadClip(x))
								 .ToList(),
				editorExpanded = true,
				loop = false,
				pauseable = true,
				pitch = 1,
				volume = 0.8f
			};
		}
	}
}