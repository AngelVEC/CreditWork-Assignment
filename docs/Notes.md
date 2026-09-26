Disclaimer, this Project was created with the help of Claude free plan.

# Day 1: Planning
When I got the assigment task about this project, Firstly I read all the requirements needed for this assignment.
and then I converse with claude regarding the document, and asking him to create `Build_instruction.md` where it will be used as foundation of the application that I will create.
Some of the key topic about our conversation:
- Database Architecture & Schema
- Using React as Frontend
- Logic behind Vehicle categories
- Using Icon from react packages, so it doesn't required us to upload image for icons
- Explicitly said to use SQL express (because of lightweight)
- Designing admin page and login page for admin (this not required on the task), The reason I created this because it will looks weird, if there is someone can edit the database and change information of the website without authentication. I made the authentication simple with just JWT token that saved on the cookies to check whether the admin is legitimate with backend check.
- Explicitly ask Claude to wrote in the document, that all sensitive information and credentials need to be saved as environment variable, instead of hardcoded.
All the conversation that happen on that session can be found in `CreditWorks_App_Planning_Transcript.md`.

# Day 2: Building
I asked Claude to create the apps based on `CreditWorks_App_Planning_Transcript.md`, and using the first generated code as the base.
while looking at the code, I found some problem that required few attention, and fixed it along the way, some of them such as:
- Populating the database with some vehicle datas, instead of just empty on first build.
- Changing some logic on vehicle weight categories, where it will automaticaly change the value of other categories if it intersect with other categories.
example: Light has range of 0-500 kg (it will start from 0 to 499 kg)
Medium has range of 500-1500kg (it will start from 500 to 1499 kg)
if we change the value of `light` from 0 to 600kg, `medium` category will also automatically change to 600-1500kg
- Adding live search function on the main page
- Whenever sorting icon clicked, it will ask the backend to send new information about the vehicle list. (there is also another approach that we can do, for example doing the sorting on the frontend)
- Fixed a bug where the input box on categories setting is non-modifable after the user typed something that is not a number.
- Added a confirmation in the frontend, whenever the value of vehicle weight categories intersect with other categories, before it got send to the backend.