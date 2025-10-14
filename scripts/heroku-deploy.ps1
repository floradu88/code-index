# Container deploy to Heroku (example)
heroku login
heroku container:login
$AppName = $env:HEROKU_APP_NAME
docker build -t registry.heroku.com/$AppName/web .
docker push registry.heroku.com/$AppName/web
heroku container:release web -a $AppName
