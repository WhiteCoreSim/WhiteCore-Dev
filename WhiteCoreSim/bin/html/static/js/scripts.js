$(function() {


	/***************** Tooltips ******************/
    $('[data-toggle="tooltip"]').tooltip();

	/***************** Nav Transformicon ******************/

	/* When user clicks the Icon */
	$('.nav-toggle').click(function() {
		$(this).toggleClass('active');
		$('.header-nav').toggleClass('open');
		event.preventDefault();
	});
	/* When user clicks a link */
	$('.header-nav li a').click(function() {
		$('.nav-toggle').toggleClass('active');
		$('.header-nav').toggleClass('open');

	});

	/* When user clicks the sidebar Icon */
	$('#toggleSideNav').click(function() {
		$('body').toggleClass('sbheader-visible');
		event.preventDefault();
	});

	/***************** Header BG Scroll ******************/


	$(window).scroll(function() {
		var scroll = $(window).scrollTop();

		if (scroll >= 20) {
			$('section.navigation').addClass('fixed');
			$('section.top-navigation').addClass('fixed');
			$('header').css({
				"border-bottom": "none",
				"background-color": "rgba(34, 34, 34, 0.55)"
				//"padding": "35px 0px 5px"
			});
			$('header .member-actions').css({
				"top": "5px",
			});
			$('header .navicon').css({
				"top": "34px",
			});
		} else {
			$('section.navigation').removeClass('fixed');
			$('section.top-navigation').removeClass('fixed');
			$('header').css({
				"border-bottom": "solid 1px rgba(255, 255, 255, 0.2)",
				"background-color": "transparent"
				//"padding": "40px 0px 5px 0px"
			});
			$('header .member-actions').css({
				"top": "5px",
			});
			$('header .navicon').css({
				"top": "48px",
			});
		}
	});
	
	/***************** Smooth Scrolling ******************/
	/*
	$(function() {

		$('a[href*=\\#]:not([href=\\#])').click(function() {
			if (location.pathname.replace(/^\//, '') === this.pathname.replace(/^\//, '') && location.hostname === this.hostname) {

				var target = $(this.hash);
				target = target.length ? target : $('[name=' + this.hash.slice(1) + ']');
				if (target.length) {
					$('html,body').animate({
						scrollTop: target.offset().top
					}, 2000);
					return false;
				}
			}
		});

	});
	*/
});


/***************** WhiteCore specific ******************/

/* submit a form */
function submitupdate(formname, parms="", menuid="") {
  event.preventDefault();

  var $form = $("#" + formname);
  var url = $form.attr("action");
  var formdata = $form.serialize();
  formdata = formdata + "&Submit=update";
  if (parms != "") {
  	formdata = formdata + "&" + parms;
  }

  $.post(url, formdata, function( msg ) {
  	if (msg.substring(0, 1) == '!') {
      var emsg = msg.slice(1);
      MsgError(emsg, 4000, 0);
    } else {
      MsgSuccess(msg, 3000,0);
      if (menuid != "") {
      	setTimeout(function() {
      		loadusercontent(menuid);
      	}, 1000);
      }
    }
  });
  
  event.stopPropagation();

};


function submitpagesearch(formname, destdiv) {
  // submit form details and post return data to 'destdiv'
  event.preventDefault();

  var $form = $("#" + formname);
  var url = $form.attr("action");
  var formdata = $form.serialize();
  formdata = formdata + "&search=true";

  $.post( url, formdata, function( data ) {
    if (data.substring(0, 1) == '!') {
      var emsg = data.slice(1);
      MsgError(emsg, 4000, 0);
    } else {
      $("#" + destdiv).empty().append(data);
    }
  });

  event.stopPropagation();

};


/* go to user home/dashboard */
function gohome() {
  //event.stopPropagation();
  loadusercontent('user-userhome');
}
