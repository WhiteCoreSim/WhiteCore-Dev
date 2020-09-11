$(function(){
	//References
	var topmenu = $("#topmenu li");
	var usermenu = $("#sidemenu li");
	var members = $("#member-actions li");
	var loading = $("#loading");
	var content = $("#content");

	// Top menu
	topmenu.click(function(event){
		showLoading();
		switch(this.id){
		{MenuItemsArrayBegin}
			case "{MenuItemID}":
		    content.load("{MenuItemLocation}" + window.location.search, hideLoading);
				break;
		{MenuItemsArrayEnd}
			default:
				//hide loading bar if there is no selected section
				hideLoading();
				break;
		}
		event.stopPropagation();
	});

	// User menu
	usermenu.click(function(event){
		showLoading();
		switch(this.id){
		{UserMenuItemsArrayBegin}
			case "{MenuItemID}":
		    content.load("{MenuItemLocation}" + window.location.search, hideLoading);
				break;
		{UserMenuItemsArrayEnd}
			default:
				//hide loading bar if there is no selected section
				hideLoading();
				break;
		}
		event.stopPropagation();
	});

	// logout
	members.click(function(event){
		switch(this.id){
		{MenuItemsArrayBegin}
			case "{MenuItemID}":
		    content.load("{MenuItemLocation}" + window.location.search, hideLoading);
				break;
		{MenuItemsArrayEnd}
			default:
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
function loadusercontent(pageid, params=''){

	var content = $("#content");
	if (params!= '') {
		params = "?" + params;
	}
	content.html('');

	// load selected page
	switch(pageid){
		{UserMenuItemsArrayBegin}
		case "{MenuItemID}":
			//console.log('loading {MenuItemLocation}')
	    content.load("{MenuItemLocation}" + params + window.location.search);
			break;
		{UserMenuItemsArrayEnd}
		default:
			break;
	}

}

function loadmodalcontent(pageid, params='', title=''){

	var content = $("#modalcontent");
	if (params!= '') {
		params = "?" + params;
	}
	content.html('');		// clear anything previously loaded

	//load selected page
	switch(pageid){
		{ModalItemsArrayBegin}
		case "{MenuItemID}":
	    content.load("{MenuItemLocation}" + params + window.location.search);
			break;
		{ModalItemsArrayEnd}
		default:
			break;
	}
  //event.stopPropagation();

	$("#profileModal").modal("show");
};
