default
{
	state_entry()
	{
		// Sends the text to all users in the entire WhiteCore.exe instance (regardless of the region they are in)
		aaAllRegionInstanceSay(0, "Hi, everyone! This is being sent to all users in the entire instance.");
	}
}
