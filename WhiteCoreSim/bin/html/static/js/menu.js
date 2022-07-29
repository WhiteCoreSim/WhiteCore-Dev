$(function(){
	// References
	var topmenu = $("#topmenu li");
	var members = $("#member-actions li");
	var loading = $("#loading");
	var main_content = $("#main_content");

	// Main menu click events
	topmenu.click(function(event){
		showLoading();
		switch(this.id){
			{MenuItemsArrayBegin}
			case "{MenuItemID}":
		    main_content.load("{MenuItemLocation}" + window.location.search, hideLoading);
				/* main_content.slideUp('swing',  function() {
				    main_content.load("{MenuItemLocation}" + window.location.search, hideLoading);
				    main_content.slideDown();
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
		    main_content.load("{MenuItemLocation}" + window.location.search, hideLoading);
				/* main_content.slideUp('swing',  function() {
				    main_content.load("{MenuItemLocation}" + window.location.search, hideLoading);
				    main_content.slideDown();
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
function loadcontent(pageid, params=''){
	var main_content = $("#main_content");
	if (params!= '') {
		params = "?" + params;
	}
	//load selected page
	switch(pageid){
		{MenuItemsArrayBegin}
			case "{MenuItemID}":
	    	main_content.load("{MenuItemLocation}" + params + window.location.search);
				break;
		{MenuItemsArrayEnd}
		default:
			break;
	}
};

function loadmodalcontent(pageid, params='', title='', static=false){
	var modal_content = $("#modalcontent");
	if (params!= '') {
		params = "?" + params;
	}
	modal_content.html('');		// clear anything previously loaded

	//load selected page
	switch(pageid){
		{ModalItemsArrayBegin}
			case "{MenuItemID}":
		    modal_content.load("{MenuItemLocation}" + params + window.location.search);
				break;
		{ModalItemsArrayEnd}
		default:
			break;
	}

  if (static) {
	  $("#profileModal").modal({
	    backdrop: "static",
	    keyboard: false
	  });
	}
	$("#profileModal").modal("show");
};
