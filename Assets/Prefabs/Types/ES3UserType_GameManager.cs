using System;
using UnityEngine;

namespace ES3Types
{
	/*[UnityEngine.Scripting.Preserve]
	[ES3PropertiesAttribute("timeFlow", "isstart", "isRobotencounter", "isRobotmove", "isFirstArmed", "isLastArmed")]
	public class ES3UserType_GameManager : ES3ComponentType
	{
		public static ES3Type Instance = null;

		public ES3UserType_GameManager() : base(typeof(GameManager)){ Instance = this; priority = 1;}


		protected override void WriteComponent(object obj, ES3Writer writer)
		{
			var instance = (GameManager)obj;
			
			writer.WriteProperty("timeFlow", GameManager.timeFlow, ES3Type_int.Instance);
			writer.WriteProperty("isstart", GameManager.isstart, ES3Type_bool.Instance);
			writer.WriteProperty("isRobotencounter", GameManager.isRobotencounter, ES3Type_bool.Instance);
			writer.WriteProperty("isRobotmove", GameManager.isRobotmove, ES3Type_bool.Instance);
			writer.WriteProperty("isFirstArmed", GameManager.isFirstArmed, ES3Type_bool.Instance);
			writer.WriteProperty("isLastArmed", GameManager.isLastArmed, ES3Type_bool.Instance);
		}

		protected override void ReadComponent<T>(ES3Reader reader, object obj)
		{
			var instance = (GameManager)obj;
			foreach(string propertyName in reader.Properties)
			{
				switch(propertyName)
				{
					
					case "timeFlow":
						GameManager.timeFlow = reader.Read<System.Int32>(ES3Type_int.Instance);
						break;
					case "isstart":
						GameManager.isstart = reader.Read<System.Boolean>(ES3Type_bool.Instance);
						break;
					case "isRobotencounter":
						GameManager.isRobotencounter = reader.Read<System.Boolean>(ES3Type_bool.Instance);
						break;
					case "isRobotmove":
						GameManager.isRobotmove = reader.Read<System.Boolean>(ES3Type_bool.Instance);
						break;
					case "isFirstArmed":
						GameManager.isFirstArmed = reader.Read<System.Boolean>(ES3Type_bool.Instance);
						break;
					case "isLastArmed":
						GameManager.isLastArmed = reader.Read<System.Boolean>(ES3Type_bool.Instance);
						break;
					default:
						reader.Skip();
						break;
				}
			}
		}
	}


	public class ES3UserType_GameManagerArray : ES3ArrayType
	{
		public static ES3Type Instance;

		public ES3UserType_GameManagerArray() : base(typeof(GameManager[]), ES3UserType_GameManager.Instance)
		{
			Instance = this;
		}
	}
	*/
}