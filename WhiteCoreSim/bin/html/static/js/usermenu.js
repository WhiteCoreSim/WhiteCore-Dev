$(function(){
	//References
	var topmenu = $("#topmenu li");
	var usermenu = $("#sidemenu li");
	var members = $("#member-actions li");
	var loading = $("#loading");
	var usr_content = $("#usr_content");

	// Top menu
	topmenu.click(function(event){
		showLoading();
		switch(this.id){
		{MenuItemsArrayBegin}
			case "{MenuItemID}":
		    usr_content.load("{MenuItemLocation}" + window.location.search, hideLoading);
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
		    usr_content.load("{MenuItemLocation}" + window.location.search, hideLoading);
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
		    usr_content.load("{MenuItemLocation}" + window.location.search, hideLoading);
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

	var user_content = $("#usr_content");
	if (params!= "") {
		params = "?" + params;
	}
	user_content.html("");

	// load selected page
	switch(pageid){
		{UserMenuItemsArrayBegin}
		case "{MenuItemID}":
	    user_content.load("{MenuItemLocation}" + params + window.location.search);
			break;
		{UserMenuItemsArrayEnd}
		default:
			break;
	}

}

function loadmodalcontent(pageid, params='', title='', static=false){
	// load modal content
	if (params != '') {				// must have some parameters here
		params = "?" + params;		// add query
	}

	var usrmodal_content = $("#modalcontent");
	usrmodal_content.html('');		// clear anything previously loaded

	//load selected page
	switch(pageid){
		{ModalItemsArrayBegin}
		case "{MenuItemID}":
	    usrmodal_content.load("{MenuItemLocation}" + params + window.location.search);
			break;
		{ModalItemsArrayEnd}
		default:
			break;
	}
	//event.stopPropagation();
	if (static) {
    $("#profileModal").modal({
      backdrop: "static",
      keyboard: false
    });
	}

	$("#profileModal").modal("show");
};
