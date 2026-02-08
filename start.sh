#!/bin/bash

# Iniciar backend
cd PolaperLinku.Api && dotnet run &
BACKEND_PID=$!

# Esperar a que el backend arranque
sleep 5

# Iniciar frontend
cd ../polaper-linku-web && npm run dev &
FRONTEND_PID=$!

echo "Backend (PID: $BACKEND_PID) corriendo en http://localhost:5000"
echo "Frontend (PID: $FRONTEND_PID) corriendo en http://localhost:5173"

# Esperar Ctrl+C
trap "kill $BACKEND_PID $FRONTEND_PID" EXIT
wait
