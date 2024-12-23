pipeline {
    agent any

    stages {
        stage('Checkout') {
            steps {
                sh 'pwd'
            }
        }

        stage('Build and Deploy') {
            steps {
                sh 'cd /home/alets/picksports && docker compose rm -f && docker compose pull && docker compose build --no-cache && docker compose up --build -d' 
                sh 'pwd'
            }
        }
    }
}
