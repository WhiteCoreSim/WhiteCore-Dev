function bgImgRotate()
{
  var images = new Array(
  	"/static/screenshots/welcome1.jpg",
  	"/static/screenshots/welcome2.jpg",
  	"/static/screenshots/welcome3.jpg",
  	"/static/screenshots/welcome4.jpg",
  	"/static/screenshots/welcome5.jpg",
  	"/static/screenshots/welcome6.jpg",
  	"/static/screenshots/welcome7.jpg",
  	"/static/screenshots/welcome8.jpg",
  	"/static/screenshots/welcome9.jpg",
  	"/static/screenshots/welcome10.jpg"
  );

  var len = images.length;
  var img_no = Math.floor(Math.random());
  img_no = Math.floor(Math.random()*len);

  var imageurl = "url(" + images[img_no] + ")";

  $(".welcomescreen").css("background-image", imageurl);
  $(".welcomescreen").css("background-size", "120%");
}

function closeSurvey(div_id)
{
  document.getElementById(div_id).style.display = "none";
}

function locationTextColor(){
	if ((document.getElementById('specifyLocation').checked == 1) && !(document.getElementById('specificLocation').value == 'Region Name')) {
		document.getElementById('specificLocation').style.color = '#FFFFFF';
	} else {
		document.getElementById('specificLocation').style.color = '#666666';
	}
}

function selectRegionRadio(){
	document.getElementById('specifyLocation').checked = 1;
}


function CheckFieldsNotEmpty(){
	var mUsername = document.getElementById('firstname_input');
	var mLastname = document.getElementById('lastname_input');
	var mPassword =document.getElementById('password_input');
	var myButton = document.getElementById('conbtn');

	if (( mUsername.value != "") && (mLastname.value != "") && (mPassword.value != "") )
	{
			myButton.disabled = false;
			myButton.className = "input_over";
	}else
	{
		myButton.disabled = true;
		myButton.className = "pressed";
	}
}

// disable backgroud image 
//window.onload=bgImgRotate();
