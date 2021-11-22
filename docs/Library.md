# Control port library

This PHP library allows you to easily communicate with the control port and build your own management tools around it.

the library is located in the PHP folder.

## Usage

```php
require('H2S.php');
$handle=H2S_connect() or die('Connection failed');
H2S_auth($handle,'Password or cookie file content') or die('Authentication failed');
//Do more here
H2_close($handle);
```

## Function overview

The file is quite short, less than 150 lines.
To get a list of functions, it's best you have a look at the file.
All functions start with `H2S_` which makes using it with the autocomplete of your editor quite easy.
A `test.php` is provided that shows how to add and remove entries in the blacklist.
