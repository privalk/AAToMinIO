pipeline {
  agent {
    node {
      label 'nodejs'
    }
  }
  environment {
    UPM_URL = 'upm.cloud-production-platform.svc.cluster.local:4873'
    NPM_AUTH_TOKEN = 'kwJXGTTUTp3sOZK+81bR/Q=='
  }
  
  stages {
    stage('npm login') {
      steps {
        container('nodejs') {
          sh 'npm config set //$UPM_URL/:_authToken $NPM_AUTH_TOKEN'
        }
      }
     }
     stage('npm publish') {
      steps {
        container('nodejs') {
          sh 'npm publish --registry http://$UPM_URL'
        }
      }
    }

   }

}
