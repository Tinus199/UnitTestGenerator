This code analyzer allows the user to generate an NUnit template for a selected class. 
This template will be generated with a Mock created for each paramater of the constructor. 
This project is in an initial release phase, with more features to be released in due time.

Known issues:
1) Code generates immediately without showing preview or clicking to fix.
2) Only generates based on the first constructor

Improvement List:
1) Allowing developer to select where the test template must be placed.
2) Automatically updating the namespace for the test template location.
3) Create variables for base types in constructor param list instead of mocks (i.e. int, double, bool etc).
