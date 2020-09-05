# XAMPP.WATCHDOG
Why I need to write this?
* Sometimes you want to host a website or anything on a WINDOWS (Not Windows Server)
* Yup, guess what? Windows Updates are here! And it kills all your web thing and restart automatically!
* This sucks so lets disable the windows update and keep on watching our services are running.

## Features:
* Auto start at windows startup
* Developed in .NET 2.0, so high support range
* Disable windows update
* Auto backup MySQL database everyday (midnight 12 am. Will kill the whole xampp for backup then restart it again. You can disable this by adding --D as arguments)
* Auto restart if any apache or mysql is closed. Keep on trying to check port is open to make sure users are really able to visit the site
* If port is not accessible, auto restart the apache and mysql to make sure both of them aren't in error

> Useless tips: Try use windows server if you can! Y U SO DUMP?!

### Used Nuget
> * ini-parser.2.5.2