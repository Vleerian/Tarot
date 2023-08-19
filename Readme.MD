# Tarot
Tarot is a multitool application for NSCards. It can help manage your puppets, as well as generate HTML in the style of tools like the RCES puppet_links_sheet

# Puppet List Format
Tarot imports a list of the user's puppets and passwords to fetch information from private shards.

Upon importing, Tarot makes a private shard `ping` request to get X-AutoLogin and only stores the hashed passwords

Despite this, **please** take care not to distribute your DeckDB unless you know you can trust the other person **with your life**

Tarot accepts puppet links in a CSV format. One puppet per line, puppets and passwords separated by comma.
```
puppet, password
puppet2, password2
...
...
```

# Credit
dithpri for creating RCES, and specifically the puppet links sheet
https://github.com/dithpri/RCES/tree/master/puppet_links_sheet

9003 for creating many useful tools. JunkDaJunk in particular directly inspired a function of Tarot
https://github.com/jmikk/Card-Proccessor/