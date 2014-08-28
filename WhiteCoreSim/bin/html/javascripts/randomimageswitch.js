function bgImgRotate() 
{ 
	var images = new Array(
  "../images/screenshots/welcome1.jpg",
  "../images/screenshots/welcome2.jpg",
  "../images/screenshots/welcome3.jpg",
  "../images/screenshots/welcome4.jpg",
  "../images/screenshots/welcome5.jpg",
  "../images/screenshots/welcome6.jpg",
  "../images/screenshots/welcome7.jpg",
  "../images/screenshots/welcome8.jpg",
  "../images/screenshots/welcome9.jpg",
  "../images/screenshots/welcome10.jpg"
  );
	
  var len = images.length;
  var img_no = Math.floor(Math.random());
  img_no = Math.floor(Math.random()*len);

	document.getElementById('mainImage').src = images[img_no]; 
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

window.onload=bgImgRotate;