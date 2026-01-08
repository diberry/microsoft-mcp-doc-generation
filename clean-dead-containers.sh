#!/bin/bash

# Script to delete all dead Docker containers

echo "Deleting all dead Docker containers..."
docker container prune -f

echo "Done! All dead containers have been removed."
