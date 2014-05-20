$(document).ready(function(){
	//References
	var sections = $("#menuregion li");
	var loading = $("#loading");
	var content = $("#content");

	//Manage click events
	sections.click(function(){
		//show the loading bar
		showLoading();
		//load selected section
		switch(this.id){
			case "region":
				content.slideUp( function() {
				content.load("base.html" + window.location.search, hideLoading);
				content.slideDown();
				});
				break;
			case "parcels":
				content.slideUp( function() {
				content.load("parcels.html" + window.location.search, hideLoading);
				content.slideDown();
				});
				break;
			case "owner":
				content.slideUp( function() {
				content.load("owner.html", hideLoading);
				content.slideDown();
				});
				break;
			default:
				//hide loading bar if there is no selected section
				hideLoading();
				break;
		}
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