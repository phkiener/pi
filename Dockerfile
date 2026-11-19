FROM node:24-alpine

RUN apk add --no-cache bash ca-certificates git ripgrep curl fd
RUN npm install -g --ignore-scripts @earendil-works/pi-coding-agent

WORKDIR /workspace
ENTRYPOINT ["pi"]
