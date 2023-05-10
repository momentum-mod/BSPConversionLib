﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BSPConvert.Lib
{
	public class ShaderParser
	{
		private string shaderFile;

		public ShaderParser(string shaderFile)
		{
			this.shaderFile = shaderFile;
		}

		public Dictionary<string, Shader> ParseShaders()
		{
			var shaderDict = new Dictionary<string, Shader>();

			var fileEnumerator = File.ReadLines(shaderFile).GetEnumerator();
			string line;
			while ((line = GetNextValidLine(fileEnumerator)) != null)
			{
				var textureName = line;
				if (TryParseShader(fileEnumerator, out var shader))
					shaderDict[textureName] = shader;
			}

			return shaderDict;
		}

		private bool TryParseShader(IEnumerator<string> fileEnumerator, out Shader shader)
		{
			shader = new Shader();
			var stages = new List<ShaderStage>();

			var line = GetNextValidLine(fileEnumerator);
			if (string.IsNullOrEmpty(line) || !line.StartsWith('{'))
			{
				Debug.WriteLine("Warning: Expecting '{', found '" + line + "' instead in shader file: " + shaderFile);
				return false;
			}

			while ((line = GetNextValidLine(fileEnumerator)) != null)
			{
				if (line == "}") // End of shader definition
					break;
				else if (line.StartsWith('{'))
				{
					stages.Add(ParseStage(fileEnumerator));
					continue;
				}

				// Parse shader parameter
				var split = line.Split();
				switch (split[0].ToLower())
				{
					case "q3map_sun":
						break;
					case "deformvertexes":
						break;
					case "tesssize":
						break;
					case "clamptime":
						break;
					case "q3map":
						break;
					case "surfaceparm":
						{
							var infoParm = ParseSurfaceParm(split[1]);
							shader.surfaceFlags |= infoParm.surfaceFlags;
							shader.contents |= infoParm.contents;
							break;
						}
					case "nomipmaps":
						break;
					case "nopicmip":
						break;
					case "polygonoffset":
						break;
					case "entitymergable":
						break;
					case "fogparms":
						shader.fogParms = ParseFogParms(split);
						break;
					case "portal":
						break;
					case "skyparms":
						shader.skyParms = ParseSkyParms(split);
						break;
					case "light":
						break;
					case "cull":
						shader.cullType = ParseCullType(split[1]);
						break;
					case "sort":
						break;
					default:
						if (!split[0].ToLower().StartsWith("qer"))
							Debug.WriteLine("Warning: Unknown shader parameter '" + split[0] + "' in shader file: " + shaderFile);
						
						break;
				}
			}

			shader.stages = stages.ToArray();

			return true;
		}

		private ShaderStage ParseStage(IEnumerator<string> fileEnumerator)
		{
			var stage = new ShaderStage();

			string line;
			while ((line = GetNextValidLine(fileEnumerator)) != null)
			{
				if (line == "}") // End of shader pass definition
					break;
				
				var split = line.Split();
				switch (split[0].ToLower())
				{
					case "map":
						stage.bundles[0].images[0] = split[1];
						break;
					case "clampmap":
						break;
					case "animmap":
						stage.bundles[0] = ParseAnimMap(split);
						break;
					case "videomap":
						break;
					case "alphafunc":
						stage.flags |= ParseAlphaFunc(split[1]);
						break;
					case "depthfunc":
						break;
					case "detail":
						break;
					case "blendfunc":
						stage.flags |= ParseBlendFunc(split);
						break;
					case "rgbgen":
						break;
					case "alphagen":
						break;
					case "texgen":
					case "tcgen":
						stage.bundles[0].tcGen = ParseTCGen(split[1]);
						break;
					case "tcmod":
						break;
					case "depthwrite":
						break;
					default:
						Debug.WriteLine($"Warning: Unknown shader parameter: {split[0]}");
						break;
				}
			}

			return stage;
		}

		private TextureBundle ParseAnimMap(string[] split)
		{
			var bundle = new TextureBundle();

			if (split.Length < 3)
			{
				Debug.WriteLine("Warning: Missing parameters in shader: " + shaderFile);
				return bundle;
			}

			bundle.imageAnimationSpeed = float.Parse(split[1]);

			for (var i = 2; i < split.Length; i++)
			{
				if (bundle.numImageAnimations >= TextureBundle.MAX_IMAGE_ANIMATIONS)
					break;
				
				bundle.images[bundle.numImageAnimations] = split[i];
				bundle.numImageAnimations++;
			}

			return bundle;
		}

		private InfoParm ParseSurfaceParm(string surfaceParm)
		{
			foreach (var infoParm in Constants.infoParms)
			{
				if (surfaceParm == infoParm.name)
					return infoParm;
			}

			return default;
		}

		private Shader.FogParms ParseFogParms(string[] split)
		{
			var fogParms = new Shader.FogParms();

			if (split[1] != "(")
			{
				Debug.WriteLine("Warning: Missing parenthesis in shader: " + shaderFile);
				return null;
			}

			if (!float.TryParse(split[2], out fogParms.color.X) ||
				!float.TryParse(split[3], out fogParms.color.Y) ||
				!float.TryParse(split[4], out fogParms.color.Z))
			{
				Debug.WriteLine("Warning: Missing vector3 element in shader: " + shaderFile);
				return null;
			}

			if (split[5] != ")")
			{
				Debug.WriteLine("Warning: Missing parenthesis in shader: " + shaderFile);
				return null;
			}

			if (!float.TryParse(split[6], out fogParms.depthForOpaque))
			{
				Debug.WriteLine("Warning: Missing parm for 'fogParms' keyword in shader: " + shaderFile);
				return null;
			}

			return fogParms;
		}

		private Shader.SkyParms ParseSkyParms(string[] split)
		{
			return new Shader.SkyParms()
			{
				outerBox = split[1],
				cloudHeight = split[2],
				innerBox = split[3]
			};
		}

		private CullType ParseCullType(string cullType)
		{
			switch (cullType.ToLower())
			{
				case "none":
				case "twosided":
				case "disable":
					return CullType.TWO_SIDED;
				case "back":
				case "backside":
				case "backsided":
					return CullType.BACK_SIDED;
				default:
					return CullType.FRONT_SIDED;
			}
		}

		private ShaderStageFlags ParseAlphaFunc(string func)
		{
			switch (func.ToLower())
			{
				case "gt0":
					return ShaderStageFlags.GLS_ATEST_GT_0;
				case "lt128":
					return ShaderStageFlags.GLS_ATEST_LT_80;
				case "ge128":
					return ShaderStageFlags.GLS_ATEST_GE_80;
			}

			// Invalid alphaFunc
			return 0;
		}

		private ShaderStageFlags ParseBlendFunc(string[] split)
		{
			switch (split[1].ToLower())
			{
				case "add":
					return ShaderStageFlags.GLS_SRCBLEND_ONE | ShaderStageFlags.GLS_DSTBLEND_ONE;
				case "filter":
					return ShaderStageFlags.GLS_SRCBLEND_DST_COLOR | ShaderStageFlags.GLS_DSTBLEND_ZERO;
				case "blend":
					return ShaderStageFlags.GLS_SRCBLEND_SRC_ALPHA | ShaderStageFlags.GLS_DSTBLEND_ONE_MINUS_SRC_ALPHA;
				default:
					if (split.Length == 3)
						return ParseSrcBlendMode(split[1]) | ParseDestBlendMode(split[2]);
					else
						return 0;
			}
		}

		private ShaderStageFlags ParseSrcBlendMode(string src)
		{
			switch (src.ToUpper())
			{
				case "GL_ONE":
					return ShaderStageFlags.GLS_SRCBLEND_ONE;
				case "GL_ZERO":
					return ShaderStageFlags.GLS_SRCBLEND_ZERO;
				case "GL_DST_COLOR":
					return ShaderStageFlags.GLS_SRCBLEND_DST_COLOR;
				case "GL_ONE_MINUS_DST_COLOR":
					return ShaderStageFlags.GLS_SRCBLEND_ONE_MINUS_DST_COLOR;
				case "GL_SRC_ALPHA":
					return ShaderStageFlags.GLS_SRCBLEND_SRC_ALPHA;
				case "GL_ONE_MINUS_SRC_ALPHA":
					return ShaderStageFlags.GLS_SRCBLEND_ONE_MINUS_SRC_ALPHA;
				case "GL_DST_ALPHA":
					return ShaderStageFlags.GLS_SRCBLEND_DST_ALPHA;
				case "GL_ONE_MINUS_DST_ALPHA":
					return ShaderStageFlags.GLS_SRCBLEND_ONE_MINUS_DST_ALPHA;
				case "GL_SRC_ALPHA_SATURATE":
					return ShaderStageFlags.GLS_SRCBLEND_ALPHA_SATURATE;
				default:
					Debug.WriteLine($"Warning: Unknown blend mode: {src}");
					return ShaderStageFlags.GLS_SRCBLEND_ONE;
			}
		}

		private ShaderStageFlags ParseDestBlendMode(string dest)
		{
			switch (dest.ToUpper())
			{
				case "GL_ONE":
					return ShaderStageFlags.GLS_DSTBLEND_ONE;
				case "GL_ZERO":
					return ShaderStageFlags.GLS_DSTBLEND_ZERO;
				case "GL_SRC_ALPHA":
					return ShaderStageFlags.GLS_DSTBLEND_SRC_ALPHA;
				case "GL_ONE_MINUS_SRC_ALPHA":
					return ShaderStageFlags.GLS_DSTBLEND_ONE_MINUS_SRC_ALPHA;
				case "GL_DST_ALPHA":
					return ShaderStageFlags.GLS_DSTBLEND_DST_ALPHA;
				case "GL_ONE_MINUS_DST_ALPHA":
					return ShaderStageFlags.GLS_DSTBLEND_ONE_MINUS_DST_ALPHA;
				case "GL_SRC_COLOR":
					return ShaderStageFlags.GLS_DSTBLEND_SRC_COLOR;
				case "GL_ONE_MINUS_SRC_COLOR":
					return ShaderStageFlags.GLS_DSTBLEND_ONE_MINUS_SRC_COLOR;
				default:
					Debug.WriteLine($"Warning: Unknown blend mode: {dest}");
					return ShaderStageFlags.GLS_DSTBLEND_ONE;
			}
		}

		private TexCoordGen ParseTCGen(string tcGen)
		{
			switch (tcGen.ToLower())
			{
				case "environment":
					return TexCoordGen.TCGEN_ENVIRONMENT_MAPPED;
				case "lightmap":
					return TexCoordGen.TCGEN_LIGHTMAP;
				case "texture":
				case "base":
					return TexCoordGen.TCGEN_TEXTURE;
				case "vector":
					// TODO: Handle vector parsing
					break;
				default:
					Debug.WriteLine("Warning: Unknown texgen param in shader: " + shaderFile);
					break;
			}

			return TexCoordGen.TCGEN_TEXTURE;
		}

		// TODO: Get next valid token instead of line
		private string GetNextValidLine(IEnumerator<string> fileEnumerator)
		{
			while (fileEnumerator.MoveNext())
			{
				var line = TrimLine(fileEnumerator.Current);
				if (!string.IsNullOrEmpty(line))
					return line;
			}

			return null;
		}

		private string TrimLine(string line)
		{
			var trimmed = line.Trim();

			// Remove comments from line
			if (trimmed.Contains("//"))
				trimmed = trimmed.Substring(0, trimmed.IndexOf("//"));

			// TODO: Handle multi-line comments
			
			return trimmed;
		}
	}
}