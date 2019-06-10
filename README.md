# breach-importer
A dotnet core console application that reads multiple text files under a certain directory (from the breach compilation for example) and imports the emails and passwords int MySQL database. The contents of the files should be in the format USERNAME:PASSWORD, each individual pair on a new line.

Usage:
     BreachImporter [OPTIONS]
     example: BreachImporter --path=/home/root/breachcompilation/data --database=breach --table=user --username=myuser --password=secret
Options:
     --path
          The path to the data folder of the breach compilation
          example: --path=/home/root/breachcompilation/data
     --database
          The name of the MySql database to import to
          example: --database=breach
     --table
          The name of the MySql table to import data to (the table has to have two string columns named user and pass)
          example: --table=user
     --username
          The MySql username to use for the import process
          example: --username=breach
     --password
          The MySql Password to use for the import process
          example: --password=secret
          
To run it:
     1) Clone the git repository:
          git clone https://github.com/delianjekov/breach-importer
     2) CD into the newly downloaded directory:
          cd breach-importer
     3) Build the source code: 
          dotnet publish -c release -r ubuntu.16.04-x64
     4) (OPTIONAL) Copy the "publish" folder to its permanent directory, e.g. /opt/breach-importer
     5) Make the binary executable:
          chmod +x BreachImporter
     6) Run it:
          ./BreachImporter [OPTIONS]
          
     
