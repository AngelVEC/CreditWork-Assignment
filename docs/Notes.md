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
- Adding cascade logic into creation, edit, and delete of vehicle weight categories (Will be explained later)
- Adding new function on manufacturer where user can directly add new manufacturer when required

# Day 3: testing
- Making sure that all test case are passed (Apparently there is some of the test case created are failed, and still insepcting what is the problem, the error message can be found at `test-output.txt`)
- Found the problem that some of test has auth problem when trying to run it, will check if there any fix can be done.
- Claude keep hallucinating, because it can't run the code directly on their subsystem, and it just debugging the problem based on test-output that I generated and sent to him, while burning the token without any resolution.
- I decided to comment all the failed test that required authentication.
- Removing all the comment of failed test that required authentication, with further debugging, have found out the main problem is on the Jwt authentication in program.cs, on the previous code, some of value were assigned to local variables before it got set-up on the JWT, because of this there is a signature-key mismatch.

# Design Choice based on assumption
## Category Administration
This feature is the feature that took most of my times and claude token to finalize. at the early stage, the code on this specific features were working until I tried creating, editing, and deleting categories.
because of validation on the backend already got set-up by claude based on the markdown files, there is no way to add, edit or remove any categories because of `gaps` and `overlaps` (This was created based on assumption, and trade-off for prevent any `gaps` or `overlaps`).
After doing of some research, I decided to change a bit of my approach on this, and using cascading logic to finalize the implementation of this features.
With cascade logic, whenever the user or admin adding, editing, or deleteing categories, it will check if there is `overlaps and gaps` that might happen on other existing categories, and automatically fix the problem by readjusting the range of other categories.

Use case example based on this design:
| Category | Weight Range |
|---|---|
| Light | 0 - 500 KG |
| Medium | 500 - 2500 KG |
| Heavy | 2500 KG and Above |

1. User added new categories called `Between Light and Medium` with weight range of `400 - 900 kg `. That means it will be in the intersection of Light and Medium's weight range. The frontend will just asking user a confirmation if they want to proceed with the change, while explaining that these range that they inputted is between the range of Light and Medium's weight range. If the user clicked `Confirm`, the backend will grab the value, and automatically readjust the range value of Light and medium.
Table Preview:

| Category | Weight Range |
|---|---|
| Light | 0 - 400 KG |
| Between Light and Medium | 400 - 900 KG |
| Medium | 900 - 2500 KG |
| Heavy | 2500 KG and Above |

2. User added new categories called `Splitting medium` with weight range of `1100 - 1600 kg`. Because the range are in the middle of medium's weight range. for this special scenario, I am using new approach, called splitting three-way. The backend will automatically split medium into three part, `Medium (1) as lower bound`, `Splitting medium as the range that user inputted`, and `Medium (2) as upper bound`. With this approach, it should fix the underlying problem of any user that want to inserting range value that is within one category.
Table Preview:

| Category | Weight Range |
|---|---|
| Light | 0 - 500 KG |
| Medium (1) | 500 - 1100 KG |
| Splitting Medium | 1100 - 1600 KG |
| Medium (2) | 1600 - 2500 KG |
| Heavy | 2500 KG and Above |

3. User trying to add new categories called `medium new` with weight range of `400 - 2600 kg`. The user will get an error message, that explained that these ranged are overlapped with `medium` and not able to proceed with this change.

4. User editing the value of `Medium` from `500 - 2500kg` to `400 - 2200kg`. The backend will readjust the value of `Light` and `Heavy` automatically

| Category | Weight Range |
|---|---|
| Light | 0 - 400 KG |
| Medium | 400 - 2200 KG |
| Heavy | 2200 KG and Above |

5. User deleting `Heavy` category. The backend will automated adjust the max range of `Medium` if they are proceed with deletion of Heavy category

| Category | Weight Range |
|---|---|
| Light | 0 - 500 KG |
| Medium | 500 and Above |

6. User deleting `Medium` category while Light and Heavy category still exist. The backend will automatically adjust the maximum value of Light into maximum value of medium.

| Category | Weight Range |
|---|---|
| Light | 0 - 2500 KG |
| Heavy | 2500 and Above |

## Vehicles Manufactures
This part is quite straightforward, beside of the limited manufacturer list, I added new post method in the backend, where user able to add another manufacturer into database. and use it to register a vehicle.
The reasoning behind this is, there is a chance where there will be a new manufacturer in the future, that is not in the list. Instead of later on required the developer to manually enter the manufacturer name into the database, with this approach, it can seamslessly added by the user(admin) itself.

## Vehicle Weight Category
I am not saving any information of vehicle of category into the database, because if there is any changes on vehicles weight, that means we also need to change the category of that vehicles. That also same with the changes of `vehicle categories`, when Light, Medium, or Heavy had any changes, that means we need to do another update on the vehicle data, when there is more than million of data, that might create a bottleneck on the database because you need to search all vehicles that got affected by this change.
My approach was simple, what I do is, grabbing the information of the vehicle and the categories, when the weight are within the categories range, that will be the value of that vehicle's category.

## How weight category calculate
The weight category will be inclusive on the `minimum` value of next category
for example
| Category | Weight Range |
|---|---|
| Light | 0 - 500 KG |
| Medium | 500 - 2500 KG |
| Heavy | 2500 KG and Above |

If vehicle A has 499KG it will assigned as Light
If vehicle B has 500KG it will assigned as Medium
If vehicle C has 2499KG it will assigned as Medium
If vehicle D has 2500 KG it will assigned as Heavy

# Future improvement
## Using Agent for database orchestrator
Implement AI agent to do database CRUD, the reason is, instead of writing the old fashion way where we define a function that limited to one particular function to perform CRUD, we can automate this process by delegating the task to AI agent by connecting it to the database. But, we also need to put guardrail to make sure that AI didn't do some weird task, for example deleting entire database or accessing some information that they are not supposed to access.
We could also use the AI agent to give us a summary of the data that we are looking for with the help of MCP (Model Context Protocol), where it can produce something similar as data analyst, but less hallucinations, because its working directly on the data that provided, instead of spewing some randoms informations.
There is also trade-off from using this approach, for example token cost, and some hiccups where there is still a chance that the model will still hallucinated (it might get betters when new models coming out in few years)

## Change on the current sorting
This will depends on how many users/data needed to be processed. for example, on the current code, I don't have many data to begin with, it's more better to do the sorting by frontend side, because it can reduce the backend and database server load.
but when there is large datasets, the current method is still the best to used, where asking the backend to do the sort.