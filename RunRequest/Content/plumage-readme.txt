============================== Plumage README ==============================

FAQ: WTF is Plumage?

- "Plumage (Latin: plūma "feather") refers both to the layer of feathers that cover a bird and the pattern, colour, and arrangement of those feathers" - Wikipedia

- Other than taking this bird metaphor way too far, it's a mix of HTML, CSS, and JS that makes it way easier to start a project and have it look good

============== General Plumage Setup ==============

1. Download and install Plumage NuGet package from the Y drive

2. To get started with general formatting and styling that plumage offers, add the following to your App_Start\BundleConfig.cs file:

		bundles.Add(new ScriptBundle("~/bundles/plumage").Include(
					"~/Scripts/plumage.js"));

		bundles.Add(new StyleBundle("~/Content/plumage").Include(
					"~/Content/plumage.css"));

3. Add the bundles from 'step 2' to your Views\Shared\Layout.cshtml file.

		@Styles.Render("~/Content/plumage")
		@Scripts.Render("~/bundles/plumage")

4. At the bottom of your Views\Shared\Layout.cshtml file, add a script tag with the following content:

		<script>
			doThePlumage();
		</script>

============== How to install Sparrow navigation in your project ===============

1. Follow the directions above in the 'General Plumage Setup'

2. In your App_Start\BundleConfig.cs file, modify your ~/bundles/plumage to include navigation:
	bundles.Add(new ScriptBundle("~/bundles/plumage").Include(
				"~/Scripts/plumage.js",
				"~/Scripts/navigation.js",
				"~/Scripts/navigation-parameters.js"));

3. Delete the current navigation from your Views\Shared\Layout.cshtml page. By default, it will be right under the body tag in a div that starts like this:
	<div class="navbar navbar-inverse navbar-fixed-top">
Delete that whole div

4. Put the following lines of code where that navigation div was
	@Html.Partial("NavbarPartial")

5. In that same layout file, add the following to your script tag at the bottom:
	
    <script>
		doThePlumage();
        displayNavigation(navigationParameters);
    </script>

6. Rename navigation-parameters-sample.js to navigation-parameters.js

7. In navigation-parameters.js, rename navigationParametersSample to navigationParameters

8. Run the project to make sure nothing went wrong. If everything went right, your project is now using Sparrow navigation!

============== Configuring Sparrow Navigation ==============

- All configuration of Sparrow navigation happens in the file Scripts\navigation-parameters.js

- The navigation configuration is passed to the script that displays it through the JavaScript object in this file

- By default the navigation is the same as the default MVC navigation, but with a Sparrow logo rather than "Application Name"

- Let's look at the various parameters we can use to configure it:

	- brandingLink object: optional. Defines the furthest left "brand" link/image if you want it to be different from the Sparrow logo linking to the root of the site.

			- url: optional. URL the link should go to. Default is "/".

			- type: optional. Choices are "text" or "image". Default is image.

			- value: optional. If type is text, this is the text the link will display. If type is image, this should be the URL to the image used.

			These parameters can be combined in a variety of ways. For example, if you like the Sparrow logo but want it to link to something different, all you have to do is give it the parameter url: whatever, no type or value necessary.

	- links[] array: This is how to configure the links that'll show up in the navigation. It is an array of objects. Not required, but I don't know why you'd be using this if you didn't have links.
	  
	  Here are the parameters for one link object:

		- linkText: required. The text that will be displayed to the user.
		
		- actionName: required. The action you're linking to.
		
		- controllerName: required. The controller for the action you're linking to.
		
		- icon: optional. FontAwesome icon to use as an icon for the link. For example, icon: "fa-coffee" would result in a small coffee cup showing up next to the link text.
		
		- parameters[] array: optional. Contains any GET parameters you may want to add to any given link. Input is a list of objects with these attributes:
			
			- name: required. The name of this parameter

			- value: required. The value of this parameter.

			- Example of this: {name: "id", value: "12"} would put ?id=12 at the end of your link.
	
	- search object - This is how to optionally add a search bar to the right side of the navigation bar. Options are below:

		- enabled: required. Whether or not this search bar should be displayed

		- method: required. How this search bar should send the data to your search function. Options are "POST" and "JavaScript". Will display a button if method is POST, no button if JavaScript.

		- javascriptAction: required if method == "JavaScript". JavaScript function that should run when the onkeyup action is triggered on the search bar. Example: "method()"

		- postAction: required if method == "POST". What URL the search bar should post to when submitted. Example is "/Home/Search". The parameter you will pick up 

============== Using and Configuring Progress Bar ==============

1. Follow the directions above in the 'General Plumage Setup'

2. Open the View you want to include the progress bar on. On the bottom of the page (right above your script tag) include the progress-bar-paramteres.js file
	@Scripts.Render("~/Scripts/progress-bar-parameters.js")

3. In your <script></script> tag on your view page, include the following line (can be inside of $(document).ready() call):
	<script>
		$(document).ready(function() {
			progbarInitialize(progressParams);
		})		
	</script>

4. Create an empty <div> tag and buttons on your view page where you want the progress bar to be displayed, like so: (these can be changed later)
	<div class="sparrow-progbar"></div>
	<button id="backBtn" class="btn btn-danger">Previous Step</button>
	<button id="nextBtn" class="btn btn-success">Next Step</button>

4. Run the project to make sure nothing went wrong. If everything went right, your page should display a 4 step example progress bar! 
   Details below for customizing your progress bar...

   So obviously this isn't EXACTLY what you want your buttons and progress bar to look like. If it is, you should probably just give up. :(
   All of your controls that you'll need for the progress bar are in the Scripts/progress-bar-parameters.js file, which will let you customize the step titles, 
   number of steps, step descriptions, whether you want to display the step descriptions, and how you want to control the steps.

	-steps[] array: This allows you to control the number of steps, by adding or removing objects to the array. The progress bar will automatically resize itself based on how many items you put in this array.

		Each step object has two parameters:
		
		-stepTitle: This is the short title that will display above each step bubble/circle.

		-stepDescription: This text will display in a formatted well below the progress bar to give the user some information about the current step they're on (instructions/expectations).
						  This parameter is optional, just leave it blank and we can turn off the display of step descriptions with another option in a second here.

	-stepNavigation object: This object takes two parameters (element id's), which will determine which elements to assign click() handlers to for incrementing and decrementing the steps.
		
		-next: The element id to assign the progbarIncrementStep() click handler to.

		-previous: The element id to assign the progbarDecrementStep() click handler to.

		You can rename the id's of the buttons that were added above to correspond to these names. Both of these parameters are optional, if you would prefer not to use one or both, just leave them blank and remove the generated buttons.

	-stepDescriptionDisplay boolean: This boolean value simply enables/shows (true) or disables/hides (false) the formatted well that displays the step descriptions from the steps[] array.

	Note: The default progbar implementation is designed to be used with a single page making ajax calls to modify content outside of the progress bar. (no page reload)
		  If you need to persist the progress bar across page reloads, check out the following functions in the Scripts/plumage.js file to implement a work-around:
					progbarSetStep(step) -- takes an int param to set the current step
					progbarIncrementStep(currentStep) -- takes an int param (the current step)
					progbarDecrementStep(currentStep) -- takes an int param (the current step)

============== Using and Configuring Toast Menu ==============

The Sparrow Toast menu is an off-canvas menu on the left side of the screen that uses css transitions to flow into view in a slick manner.
The toast menu can house page controls (buttons, inputs, etc) or whatever your little dev heart desires (as long as it fits!).

1. Follow the directions above in the 'General Plumage Setup'

2. Add a <div> with the corresponding class to the page you want the Toast Menu to display on:

	<div class="sp-toast"></div>

3. BOOM! DONE! Well almost...it'll work in Chrome/Firefox, but that pesky IE wants to fight you. Don't worry we can fix that, just call the following function in your <script> tag to fight off the demon that is IE:
   Note: This actually does a little more than just fix IE, so please use it.
	
	<script>
		$(document).ready(function() {
			toastifyNoJam();
		})
	<script>

4. Add your desired content inside of the <div> element!


By default the toast menu is 370px x 510px, I found this to be an optimal width and the max height for laptop screens. If you want to override these settings it's not hard, though.
In your local .css file (likely /Content/Site.css) just add the following to modify the height: (DO NOT EDIT THE PLUMAGE FILE OR IT WILL REVERT IF PLUMAGE UPDATES)

.sp-toast {
	height: ###px !important;			<-- Replace ### with your desired size
}

To edit the width, similarly just add the following to your local css file:

.sp-toast:hover {
	width: ###px !important;			<-- Replace ### with your desired size
}

============== Using THE Spinner ==============

A neat little animated spinner for when your page needs just a second longer...

1. Follow the directions above in the 'General Plumage Setup'

2. This one's an all CSS solution, so just add a <div> with the corresponding class wherever you want the spinner:

	<div class="sparrow-spinner"></div>

3. Spin on!