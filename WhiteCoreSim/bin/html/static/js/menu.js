$(document).ready(function(){
	// References
	var topmenu = $("#topmenu li");
	var members = $("#member-actions li");
	var loading = $("#loading");
	var content = $("#content");

	// Main menu click events
	topmenu.click(function(event){
		showLoading();
		switch(this.id){
			{MenuItemsArrayBegin}
			case "{MenuItemID}":
		    content.load("{MenuItemLocation}" + window.location.search, hideLoading);
				/* content.slideUp('swing',  function() {
				    content.load("{MenuItemLocation}" + window.location.search, hideLoading);
				    content.slideDown();
				}); */

				break;
				{MenuItemsArrayEnd}
			default:
				// hide loading bar if there is no selected section
				hideLoading();
				break;
		}
		event.stopPropagation();
	});

	// member action events - login
	members.click(function(event){
		//show the loading bar
		//showLoading();
		//load selected section
		switch(this.id){
		 	{MenuItemsArrayBegin}
			case "{MenuItemID}":
		    content.load("{MenuItemLocation}" + window.location.search, hideLoading);
				/* content.slideUp('swing',  function() {
				    content.load("{MenuItemLocation}" + window.location.search, hideLoading);
				    content.slideDown();
				}); */

				break;
				{MenuItemsArrayEnd}
			default:
				//hide loading bar if there is no selected section
				//hideLoading();
				break;
		}
		event.stopPropagation();
	});

	//show loading bar
	function showLoading(){
		loading
			.css({visibility:"visible"})
			.css({opacity:"1"})
			.css({display:"block"})
		;
	}
	//hide loading bar
	function hideLoading(){
		loading.fadeTo(1000, 0);
	};
});

// embedded page content
function loadcontent(pageid){
	var content = $("#content");

	//load selected page
	switch(pageid){
		{MenuItemsArrayBegin}
		case "{MenuItemID}":
	    content.load("{MenuItemLocation}" + window.location.search);
			break;
		{MenuItemsArrayEnd}
		default:
			break;
	}
};
