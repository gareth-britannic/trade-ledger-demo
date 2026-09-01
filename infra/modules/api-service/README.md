# API service module

Creates a private ECS Fargate API service with `awsvpc` networking, CloudWatch
logging, container insights, deployment rollback, and separate execution and
application task roles. The task role can only publish to the supplied fill
queue ARN.

ECS Exec is disabled so the application role remains limited to queue
publishing. The reference uses a public-registry image, so the execution role
needs no ECR permissions. It can write only to this service's log streams and
read only the supplied database secret.
