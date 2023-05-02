# Palace Pal

## Server Installation

To run your own server, compile this plugin in DEBUG mode, load it as a dev plugin and configure the server as follows:

```sh
# create the directory for the sqlite db & some keys
mkdir data

# generate a random key (don't need to use openssl, any other base64 string is fine)
openssl rand -base64 48 > data/jwt.key

# start the server
docker run -it --rm -v "$(pwd)/data:/data" -p 127.0.0.1:5415:5415 ghcr.io/carvelli/palace-pal
```
