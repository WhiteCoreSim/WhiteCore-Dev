printList(list keys, list values)
{
	integer length = llGetListLength(keys);
	integer i = 0;
	for(i = 0; i < length; i++)
	{
		llSay(0, llList2String(keys, i) + " - " +
			llList2String(values, i));
	}
}
default
{
	state_entry()
	{
		list keys = ["WL_AMBIENT",
			"WL_SKY_BLUE_DENSITY",
			"WL_SKY_BLUR_HORIZON",
			"WL_CLOUD_COLOR",
			"WL_CLOUD_POS_DENSITY1",
			"WL_CLOUD_POS_DENSITY2",
			"WL_CLOUD_SCALE",
			"WL_CLOUD_SCROLL_X",
			"WL_CLOUD_SCROLL_Y",
			"WL_CLOUD_SCROLL_X_LOCK",
			"WL_CLOUD_SCROLL_Y_LOCK",
			"WL_CLOUD_SHADOW",
			"WL_SKY_DENSITY_MULTIPLIER",
			"WL_SKY_DISTANCE_MULTIPLIER",
			"WL_SKY_GAMMA",
			"WL_SKY_GLOW",
			"WL_SKY_HAZE_DENSITY",
			"WL_SKY_HAZE_HORIZON",
			"WL_SKY_LIGHT_NORMALS",
			"WL_SKY_MAX_ALTITUDE",
			"WL_SKY_STAR_BRIGHTNESS",
			"WL_SKY_SUNLIGHT_COLOR",
			"WL_WATER_BLUR_MULTIPLIER",
			"WL_WATER_FRESNEL_OFFSET",
			"WL_WATER_FRESNEL_SCALE",
			"WL_WATER_NORMAL_MAP",
			"WL_WATER_NORMAL_SCALE",
			"WL_WATER_SCALE_ABOVE",
			"WL_WATER_SCALE_BELOW",
			"WL_WATER_UNDERWATER_FOG_MODIFIER",
			"WL_WATER_FOG_COLOR",
			"WL_WATER_FOG_DENSITY",
			"WL_WATER_BIG_WAVE_DIRECTION",
			"WL_WATER_LITTLE_WAVE_DIRECTION"];
		list values = [WL_AMBIENT,
			WL_SKY_BLUE_DENSITY,
			WL_SKY_BLUR_HORIZON,
			WL_CLOUD_COLOR,
			WL_CLOUD_POS_DENSITY1,
			WL_CLOUD_POS_DENSITY2,
			WL_CLOUD_SCALE,
			WL_CLOUD_SCROLL_X,
			WL_CLOUD_SCROLL_Y,
			WL_CLOUD_SCROLL_X_LOCK,
			WL_CLOUD_SCROLL_Y_LOCK,
			WL_CLOUD_SHADOW,
			WL_SKY_DENSITY_MULTIPLIER,
			WL_SKY_DISTANCE_MULTIPLIER,
			WL_SKY_GAMMA,
			WL_SKY_GLOW,
			WL_SKY_HAZE_DENSITY,
			WL_SKY_HAZE_HORIZON,
			WL_SKY_LIGHT_NORMALS,
			WL_SKY_MAX_ALTITUDE,
			WL_SKY_STAR_BRIGHTNESS,
			WL_SKY_SUNLIGHT_COLOR,
			WL_WATER_BLUR_MULTIPLIER,
			WL_WATER_FRESNEL_OFFSET,
			WL_WATER_FRESNEL_SCALE,
			WL_WATER_NORMAL_MAP,
			WL_WATER_NORMAL_SCALE,
			WL_WATER_SCALE_ABOVE,
			WL_WATER_SCALE_BELOW,
			WL_WATER_UNDERWATER_FOG_MODIFIER,
			WL_WATER_FOG_COLOR,
			WL_WATER_FOG_DENSITY,
			WL_WATER_BIG_WAVE_DIRECTION,
			WL_WATER_LITTLE_WAVE_DIRECTION];

		if(aaWindlightGetSceneIsStatic())
		{
			rotation ambient = <0.214047,0.242463,0.330004,0.110012>;
			llSay(0,"Changing ambient to " + ambient);

			aaWindlightSetScene([WL_AMBIENT, ambient]);
			printList(keys, aaWindlightGetScene(values));
		}
		else
		{
			//Otherwise, pass the key of the daycycle frame you want to get the values of

			//The max amount of day cycle keyframes there are
			integer dayCycleKeyFrames = aaWindlightGetSceneDayCycleKeyFrameCount();

			rotation ambient = <0.214047,0.242463,0.330004,0.110012>;
			llSay(0,"There are " + dayCycleKeyFrames + " day cycle keyframes, changing frame " + (dayCycleKeyFrames - 1) + " ambient to " + ambient);

			aaWindlightSetScene(dayCycleKeyFrames - 1, [WL_AMBIENT, ambient]);

			//Get the last keyframe
			printList(keys, aaWindlightGetScene(dayCycleKeyFrames - 1, values));
		}
	}
}